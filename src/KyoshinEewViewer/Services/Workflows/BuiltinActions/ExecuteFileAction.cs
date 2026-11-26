using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Scriban;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public partial class ExecuteFileAction : WorkflowAction
{
	private static JsonSerializerOptions JsonSerializerOptions { get; } = new()
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
		},
	};

	[JsonIgnore]
	public override Control DisplayControl => new ExecuteFileActionControl() { DataContext = this };

	[ObservableProperty]
	public partial string FilePath { get; set; } = "";

	[ObservableProperty]
	public partial string WorkingDirectory { get; set; } = "";

	[ObservableProperty]
	public partial bool UseShellExecute { get; set; } = true;

	[ObservableProperty]
	public partial string Arguments { get; set; } = "";

	[ObservableProperty]
	public partial bool WaitToEnd { get; set; } = true;

	/// <summary>
	/// コンソールアプリケーションのウィンドウを表示する (Windows のみ)
	/// </summary>
	[ObservableProperty]
	public partial bool ShowWindow { get; set; }

	[JsonIgnore]
	[ObservableProperty]
	public partial string LatestExecuteResult { get; set; } = "";

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		if (string.IsNullOrWhiteSpace(FilePath))
			return;

		// テンプレートをレンダリング
		var renderedFilePath = (await Template.Parse(FilePath).RenderAsync(content, m => m.Name)).Trim().Replace("\n", "");
		var renderedArguments = (await Template.Parse(Arguments).RenderAsync(content, m => m.Name)).Trim();
		var renderedWorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory)
			? ""
			: (await Template.Parse(WorkingDirectory).RenderAsync(content, m => m.Name)).Trim().Replace("\n", "");

		var info = CreateProcessStartInfo(renderedFilePath, renderedArguments, renderedWorkingDirectory);
		// 利用条件で外部への送信が禁止されている情報は環境変数で渡さない
		// 親プロセスから継承した値も残さないよう削除する
		if (content.IsExternalRelayProhibited)
			info.EnvironmentVariables.Remove("KEVI_EVENT_DATA");
		else
			info.EnvironmentVariables["KEVI_EVENT_DATA"] = JsonSerializer.Serialize(content, JsonSerializerOptions);

		var task = Task.Run(() =>
		{
			try
			{
				var sw = Stopwatch.StartNew();
				var process = Process.Start(info);
				if (process == null)
				{
					LatestExecuteResult = "プロセスの起動に失敗しました";
					return;
				}
				LatestExecuteResult = "実行中...";
				process.WaitForExit();
				LatestExecuteResult = $"終了コード: {process.ExitCode}\n実行時間:{sw.ElapsedMilliseconds}ms";
			}
			catch (Exception ex)
			{
				LatestExecuteResult = "実行中に例外が発生しました。\n" + ex;
			}
		});

		if (WaitToEnd)
			await task;
	}

	private ProcessStartInfo CreateProcessStartInfo(string filePath, string arguments, string workingDirectory)
	{
		if (UseShellExecute)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return new ProcessStartInfo("cmd", "/c " + BuildStartCommand(filePath))
				{
					WorkingDirectory = workingDirectory,
					CreateNoWindow = true,
				};
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return new ProcessStartInfo("xdg-open", filePath)
				{
					WorkingDirectory = workingDirectory,
					CreateNoWindow = true,
				};
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				return new ProcessStartInfo("open", filePath)
				{
					WorkingDirectory = workingDirectory,
					CreateNoWindow = true,
				};
		}

		return new ProcessStartInfo(filePath, arguments)
		{
			WorkingDirectory = workingDirectory,
			CreateNoWindow = !ShowWindow,
		};
	}

	/// <summary>
	/// 既定のアプリで開くための start コマンドを組み立てる (Windows)
	/// </summary>
	private string BuildStartCommand(string filePath)
	{
		// start は最初の引用符付き文字列をウィンドウタイトルとして扱うため、引用符で囲まれたパスの前には空のタイトルを置く
		var command = filePath.StartsWith("\"\"") ? filePath[2..].TrimStart() : filePath;
		var title = command.StartsWith('"') ? "\"\" " : "";

		// start はバッチファイルを cmd /K で起動し、終了後も cmd が入力待ちのまま残るため、cmd /c を介して起動する
		// その場合は外側と内側の cmd で2回解釈されるので、& は2重にエスケープする
		var target = command.StartsWith('"') ? command[1..].Split('"')[0] : command.Split(' ', '\t')[0];
		command = !target.Contains("://") && Path.GetExtension(target).ToLowerInvariant() is ".bat" or ".cmd"
			? "cmd /c " + command.Replace("&", "^^^&")
			: command.Replace("&", "^&");

		// cmd 自体は常に非表示にし、/b を外すと start が起動するコンソールアプリに新しいウィンドウが割り当てられる
		return $"start {(ShowWindow ? "" : "/b ")}{title}{command}";
	}
}
