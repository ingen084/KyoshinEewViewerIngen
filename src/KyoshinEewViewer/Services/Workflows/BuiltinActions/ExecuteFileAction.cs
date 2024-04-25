using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class ExecuteFileAction : WorkflowAction
{
	private static JsonSerializerOptions JsonSerializerOptions { get; } = new()
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
		},
	};

	public override Control DisplayControl => new ExecuteFileActionControl() { DataContext = this };

	private string _filePath = "";
	public string FilePath
	{
		get => _filePath;
		set => this.RaiseAndSetIfChanged(ref _filePath, value);
	}

	private string _workingDirectory = "";
	public string WorkingDirectory
	{
		get => _workingDirectory;
		set => this.RaiseAndSetIfChanged(ref _workingDirectory, value);
	}

	private bool _useShellExecute = true;
	public bool UseShellExecute
	{
		get => _useShellExecute;
		set => this.RaiseAndSetIfChanged(ref _useShellExecute, value);
	}

	private bool _waitToEnd = true;
	public bool WaitToEnd
	{
		get => _waitToEnd;
		set => this.RaiseAndSetIfChanged(ref _waitToEnd, value);
	}

	private string _latestExecuteResult = "";
	[JsonIgnore]
	public string LatestExecuteResult
	{
		get => _latestExecuteResult;
		set => this.RaiseAndSetIfChanged(ref _latestExecuteResult, value);
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		if (string.IsNullOrWhiteSpace(FilePath))
			return;

		var info = CreateProcessStartInfo();
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

	private ProcessStartInfo CreateProcessStartInfo()
	{
		if (UseShellExecute)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return new ProcessStartInfo("cmd", $"/c start /b {FilePath.Replace("&", "^&")}")
				{
					WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? Path.GetDirectoryName(FilePath) : WorkingDirectory,
					CreateNoWindow = true,
				};
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return new ProcessStartInfo("xdg-open", FilePath)
				{
					WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? Path.GetDirectoryName(FilePath) : WorkingDirectory,
					CreateNoWindow = true,
				};
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				return new ProcessStartInfo("open", FilePath)
				{
					WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? Path.GetDirectoryName(FilePath) : WorkingDirectory,
					CreateNoWindow = true,
				};
		}

		return new ProcessStartInfo(FilePath)
		{
			WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? Path.GetDirectoryName(FilePath) : WorkingDirectory,
			CreateNoWindow = true,
		};
	}
}
