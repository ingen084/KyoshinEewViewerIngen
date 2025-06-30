using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DynamicData.Binding;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows.BuiltinTriggers;
using ReactiveUI;
using Splat;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;

public class UpdateCheckService : ReactiveObject
{
	public VersionInfo[]? AvailableUpdateVersions { get; private set; }

	private Timer CheckUpdateTask { get; }
	private HttpClient Client { get; } = new HttpClient();
	private KyoshinEewViewerConfiguration Config { get; }
	private WorkflowService WorkflowService { get; }

	private ILogger Logger { get; }

	public event Action<VersionInfo[]?>? Updated;


	private const string GithubReleasesUrl = "https://api.github.com/repos/ingen084/KyoshinEewViewerIngen/releases";
	private const string UpdateCheckUrl = "https://svs.ingen084.net/kyoshineewviewer/updates.json";
	private const string UpdatersCheckUrl = "https://svs.ingen084.net/kyoshineewviewer/updaters.json";

	public UpdateCheckService(ILogManager logManager, KyoshinEewViewerConfiguration config, WorkflowService workflowService)
	{
		SplatRegistrations.RegisterLazySingleton<UpdateCheckService>();

		Logger = logManager.GetLogger<UpdateCheckService>();
		Config = config;
		WorkflowService = workflowService;
		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEVi;" + Utils.Version);
		CheckUpdateTask = new Timer(async s =>
		{
			if (!config.Update.Enable)
				return;

			try
			{
				var currentVersion = Assembly.GetExecutingAssembly()?.GetName().Version;

				// 暫定でリクエストは残しておく
				await Client.GetStringAsync(UpdateCheckUrl);

				// 取得してでかい順に並べる
				var releases = (await GitHubRelease.GetReleasesAsync(Client, GithubReleasesUrl))
					// ドラフトリリースではなく、現在のバージョンより新しく、不安定版が有効
					.Where(r =>
						!r.Draft && (config.Update.UsePreReleaseBuild || !r.Prerelease) &&
						Version.TryParse(r.TagName, out var v) && v > currentVersion &&
						(config.Update.UseUnstableBuild || v.Build == 0))
					.OrderByDescending(r => Version.TryParse(r.TagName, out var v) ? v : new Version());

				if (!releases.Any())
				{
					AvailableUpdateVersions = null;
					Updated?.Invoke(null);
					return;
				}
				WorkflowService.PublishEvent(new UpdateAvailableEvent(AvailableUpdateVersions?.Length > 0, releases.First().TagName));
				AvailableUpdateVersions = releases.Select(r => new VersionInfo
				{
					VersionString = r.TagName + ".0",
					Time = r.CreatedAt,
					Message = r.Body,
				}).ToArray();
				Updated?.Invoke(AvailableUpdateVersions);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "UpdateCheck Error");
			}
		}, null, Timeout.Infinite, Timeout.Infinite);
		config.Update.WhenValueChanged(x => x.Enable).Subscribe(x => CheckUpdateTask.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(100)));
	}

	private bool IsUpdating { get; set; }

	private bool _isUpdateIndeterminate;
	public bool IsUpdateIndeterminate
	{
		get => _isUpdateIndeterminate;
		set => this.RaiseAndSetIfChanged(ref _isUpdateIndeterminate, value);
	}
	private double _updateProgress;
	public double UpdateProgress
	{
		get => _updateProgress;
		set => this.RaiseAndSetIfChanged(ref _updateProgress, value);
	}
	private double _updateProgressMax;
	public double UpdateProgressMax
	{
		get => _updateProgressMax;
		set => this.RaiseAndSetIfChanged(ref _updateProgressMax, value);
	}
	private string _updateState = "-";
	public string UpdateState
	{
		get => _updateState;
		set => this.RaiseAndSetIfChanged(ref _updateState, value);
	}

	/// <summary>
	/// アップデーターのプロセスを開始する
	/// </summary>
	public async Task StartUpdater()
	{
		if (IsUpdating)
			return;
		Logger.LogInfo("アップデータのセットアップを開始します");
		UpdateState = "アップデータをダウンロードします";

		IsUpdating = true;
		IsUpdateIndeterminate = true;
		try
		{
			var store = JsonSerializer.Deserialize(await Client.GetStringAsync(UpdatersCheckUrl), KyoshinEewViewerSerializerContext.Default.UpdaterStore) ?? throw new Exception("ストアをパースできません");
			var ri = RuntimeInformation.RuntimeIdentifier;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				ri = "linux-x64";
			if (!store.TryGetValue(ri, out var value))
				throw new Exception($"ストアに現在の環境 {ri} がありません");

			IsUpdateIndeterminate = false;
			var tmpFileName = Path.GetTempFileName();
			Logger.LogInfo($"アップデータをダウンロードしています: {value} -> {tmpFileName}");
			// ダウンロード開始
			await using (var fileStream = File.OpenWrite(tmpFileName))
			{
				using var response = await Client.GetAsync(value, HttpCompletionOption.ResponseHeadersRead);
				UpdateProgressMax = 100;
				var contentLength = response.Content.Headers.ContentLength ?? throw new Exception("DLサイズが取得できません");

				await using var inputStream = await response.Content.ReadAsStreamAsync();

				var total = 0;
				var buffer = new byte[1024];
				while (true)
				{
					var readed = await inputStream.ReadAsync(buffer);
					if (readed == 0)
						break;

					total += readed;
					UpdateProgress = ((double)total / contentLength) * 100;
					UpdateState = $"アップデータのダウンロード中: {total / 1024:#,0}kb / {contentLength / 1024:#,0}kb";

					await fileStream.WriteAsync(buffer.AsMemory(0, readed));
				}
			}

			IsUpdateIndeterminate = true;

			Logger.LogInfo("アップデータを展開しています");
			UpdateState = "アップデータを展開しています";

			var updaterPath = "./Updater";
			var runAs = false;

			try
			{

				if (!Directory.Exists(updaterPath))
					Directory.CreateDirectory(updaterPath);
				await Task.Run(() => ZipFile.ExtractToDirectory(tmpFileName, "./Updater", true));
			}
			catch (UnauthorizedAccessException)
			{
				// アップデータの展開に失敗したとき
				runAs = true;
				updaterPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kevi", "Updater");
				if (!Directory.Exists(updaterPath))
					Directory.CreateDirectory(updaterPath);
				await Task.Run(() => ZipFile.ExtractToDirectory(tmpFileName, updaterPath, true));
			}
			finally
			{
				File.Delete(tmpFileName);
			}

			// Windowsでない場合実行権限を付与
#if LINUX
			new Mono.Unix.UnixFileInfo("Updater/KyoshinEewViewer.Updater").FileAccessPermissions |=
					Mono.Unix.FileAccessPermissions.UserExecute | Mono.Unix.FileAccessPermissions.GroupExecute | Mono.Unix.FileAccessPermissions.OtherExecute;
#endif

			UpdateState = "アップデータを起動しています";

			// 現在の設定を保存
			ConfigurationLoader.Save(Config);

			await Task.Delay(100);

			// プロセスを起動
			var proc = new ProcessStartInfo(Path.Combine(updaterPath, "KyoshinEewViewer.Updater")) { WorkingDirectory = updaterPath };
			if (runAs)
			{
				proc.ArgumentList.Add(updaterPath);
				proc.ArgumentList.Add("run-as");
			}
			await Task.Run(() => Process.Start(proc));

			await Task.Delay(2000);

			// 自身は終了
			(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
		}
		catch (Exception ex) when (ex is InvalidDataException || ex is IOException)
		{
			Logger.LogWarning(ex, "アップデータの起動に失敗しました");
			UpdateState = "ファイルのダウンロードに失敗しました。繰り返し失敗する場合は手動での更新をお願いします。";
		}
		catch (Exception ex) when (ex is Win32Exception || ex is UnauthorizedAccessException)
		{
			Logger.LogWarning(ex, "アップデータの起動に失敗しました");
			UpdateState = "アップデータの起動に失敗しました。繰り返し失敗する場合は手動での更新をお願いします。";
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "アップデータの起動に失敗しました");
			UpdateState = "アップデートに失敗しました。繰り返し失敗する場合は手動での更新をお願いします。";
		}
		finally
		{
			IsUpdating = false;
		}
	}

	public void StartUpdateCheckTask()
		=> CheckUpdateTask.Change(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(100));
}

public class JenkinsBuildInformation
{
	[JsonPropertyName("number")]
	public int Number { get; set; }
}
