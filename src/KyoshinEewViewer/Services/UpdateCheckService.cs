using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows.BuiltinTriggers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using R3;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services;

public partial class UpdateCheckService : ObservableObject
{
	public VersionInfo[]? AvailableUpdateVersions { get; private set; }

	private Timer CheckUpdateTask { get; }
	public HttpClient Client { get; } = new();
	private KyoshinEewViewerConfiguration Config { get; }
	private WorkflowService WorkflowService { get; }

	private ILogger Logger { get; }

	public event Action<VersionInfo[]?>? Updated;


#if INTEGRATION_TEST
	// 結合テストビルド専用: 環境変数でモックサーバーへ差し替えられるようにする
	private static readonly string GithubReleasesUrl =
		Environment.GetEnvironmentVariable("KEVI_UPDATE_API_URL") ?? "https://api.github.com/repos/ingen084/KyoshinEewViewerIngen/releases";
	private static bool IsEndpointOverridden
		=> Environment.GetEnvironmentVariable("KEVI_UPDATE_API_URL") != null;
#else
	private const string GithubReleasesUrl = "https://api.github.com/repos/ingen084/KyoshinEewViewerIngen/releases";
#endif
	private const string UpdateCheckUrl = "https://svs.ingen084.net/kyoshineewviewer/updates.json";

	// RIDとGitHub Releasesアセット名のマッピング
	private static readonly Dictionary<string, string> RidToAssetMap = new()
	{
		{ "win-x64", "KyoshinEewViewer-windows-x64.zip" },
		{ "win-arm64", "KyoshinEewViewer-windows-arm64.zip" },
		{ "linux-x64", "KyoshinEewViewer-ubuntu-x64.zip" },
		{ "linux-arm64", "KyoshinEewViewer-ubuntu-arm64.zip" },
		{ "osx-x64", "KyoshinEewViewer-macos-x64.zip" },
		{ "osx-arm64", "KyoshinEewViewer-macos-arm64.zip" },
	};

	public UpdateCheckService(ILogger<UpdateCheckService> logger, KyoshinEewViewerConfiguration config, WorkflowService workflowService)
	{
		Logger = logger;
		Config = config;
		WorkflowService = workflowService;
		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEVi;" + Utils.Version);
#if INTEGRATION_TEST
		if (IsEndpointOverridden)
			Logger.LogWarning("結合テストビルド: 更新チェックのエンドポイントが差し替えられています: {GithubReleasesUrl}", GithubReleasesUrl);
#endif
		CheckUpdateTask = new Timer(async s =>
		{
			if (!config.Update.Enable)
				return;

			try
			{
				var currentVersionString = Utils.InternalVersion;
				var (currentVersion, currentSuffix) = Utils.ParseVersionString(currentVersionString);

#if INTEGRATION_TEST
				// 結合テスト時は統計用リクエストを本番サーバーへ送らない
				if (!IsEndpointOverridden)
					await Client.GetStringAsync(UpdateCheckUrl);
#else
				// 暫定でリクエストは残しておく
				await Client.GetStringAsync(UpdateCheckUrl);
#endif

				// 取得してでかい順に並べる
				var releases = (await GitHubRelease.GetReleasesAsync(Client, GithubReleasesUrl))
					// ドラフトリリースではなく、現在のバージョンより新しく、不安定版が有効
					.Where(r =>
					{
						if (r.Draft) return false;
						if (!config.Update.UsePreReleaseBuild && r.Prerelease) return false;

						var (releaseVersion, releaseSuffix) = Utils.ParseVersionString(r.TagName);

						// より新しいか同じバージョンで新しいsuffixか
						var isNewerVersion = releaseVersion > currentVersion ||
											(releaseVersion == currentVersion && Utils.IsNewerSuffix(currentSuffix, releaseSuffix));

						if (!isNewerVersion) return false;

						// 不安定版フィルター
						return config.Update.UseUnstableBuild || releaseVersion.Build == 0;
					})
					.OrderByDescending(r =>
					{
						var (v, suffix) = Utils.ParseVersionString(r.TagName);
						return (v, Utils.GetSuffixPriority(suffix));
					});

				if (!releases.Any())
				{
					AvailableUpdateVersions = null;
					Updated?.Invoke(null);
					return;
				}

				// 新しい更新情報を作成
				var newVersions = releases.Select(r => new VersionInfo
				{
					VersionString = r.TagName + ".0",
					Time = r.CreatedAt,
					Message = r.Body,
				}).ToArray();

				WorkflowService.PublishEvent(new UpdateAvailableEvent(AvailableUpdateVersions?.Length > 0, releases.First().TagName));
				AvailableUpdateVersions = newVersions;
				Updated?.Invoke(AvailableUpdateVersions);

#if INTEGRATION_TEST
				if (StartupOptions.Current?.AutoUpdateTest == true)
				{
					Logger.LogWarning("結合テストモード: 更新を検出したため自動で自己更新を開始します");
					_ = StartUpdater();
				}
#endif
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "UpdateCheck Error");
			}
		}, null, Timeout.Infinite, Timeout.Infinite);
		config.Update.ObservePropertyChanged(x => x.Enable).Subscribe(x => CheckUpdateTask.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(100)));
	}

	private bool IsUpdating { get; set; }

	[ObservableProperty]
	public partial bool IsUpdateIndeterminate { get; set; }
	[ObservableProperty]
	public partial double UpdateProgress { get; set; }
	[ObservableProperty]
	public partial double UpdateProgressMax { get; set; }
	[ObservableProperty]
	public partial string UpdateState { get; set; } = "-";

	/// <summary>
	/// 自己更新を実行する
	/// </summary>
	/// <param name="forceUpdate">バージョンチェックをスキップして強制的に更新する（動作確認用）</param>
	public async Task StartUpdater(bool forceUpdate = false)
	{
		if (IsUpdating)
			return;

		Logger.LogInformation(forceUpdate ? "強制更新を開始します" : "自己更新を開始します");
		UpdateState = "更新ファイルをダウンロードします";

		IsUpdating = true;
		IsUpdateIndeterminate = true;
		try
		{
			// 最新リリースを取得
			var releases = await GitHubRelease.GetReleasesAsync(Client, GithubReleasesUrl);
			var latestRelease = releases
				.Where(r => !r.Draft)
				.OrderByDescending(r => r.CreatedAt)
				.FirstOrDefault();

			if (latestRelease == null)
				throw new Exception("利用可能な更新が見つかりません");

			// 強制更新でない場合はバージョンチェック
			if (!forceUpdate)
			{
				var currentVersionString = Utils.InternalVersion;
				var (currentVersion, currentSuffix) = Utils.ParseVersionString(currentVersionString);
				var (releaseVersion, releaseSuffix) = Utils.ParseVersionString(latestRelease.TagName);

				var isNewerVersion = releaseVersion > currentVersion ||
									(releaseVersion == currentVersion && Utils.IsNewerSuffix(currentSuffix, releaseSuffix));

				if (!isNewerVersion)
				{
					Logger.LogInformation("最新バージョンは既にインストールされています");
					UpdateState = "最新バージョンは既にインストールされています";
					return;
				}
			}

			// 現在のRIDに対応するアセットを取得
			var (downloadUrl, expectedHash) = GetAssetDownloadUrlAndHash(latestRelease);
			if (downloadUrl == null)
				throw new Exception("現在のプラットフォーム用の更新ファイルが見つかりません");

			Logger.LogInformation("更新ファイルをダウンロードします: {DownloadUrl}", downloadUrl);

			// 更新ファイルをダウンロード
			var tempFile = await DownloadFileAsync(downloadUrl, expectedHash);

			Logger.LogInformation("自己更新処理を実行します");
			UpdateState = "更新を適用しています";
			IsUpdateIndeterminate = true;

			// プラットフォーム別の更新処理
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				await UpdateWindows(tempFile);
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				await UpdateLinux(tempFile);
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				await UpdateMacOS(tempFile);
			else
				throw new PlatformNotSupportedException("このプラットフォームは自動更新に対応していません");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "自己更新に失敗しました");
			UpdateState = "更新に失敗しました。繰り返し失敗する場合は手動での更新をお願いします。";
		}
		finally
		{
			IsUpdating = false;
		}
	}

	/// <summary>
	/// GitHub Releasesから現在のRIDに対応するアセットのダウンロードURLとハッシュを取得
	/// </summary>
	private (string? Url, string? Hash) GetAssetDownloadUrlAndHash(GitHubRelease release)
	{
		var ri = RuntimeInformation.RuntimeIdentifier;

		// Linux x64の場合はRIDを正規化
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
			ri = "linux-x64";
		// Linux ARM64の場合
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
			ri = "linux-arm64";
		// macOSの場合はアーキテクチャから判定
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			ri = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";

		if (!RidToAssetMap.TryGetValue(ri, out var assetName))
			return (null, null);

		var asset = release.Assets.FirstOrDefault(a => a.Name == assetName);
		if (asset == null)
			return (null, null);

		return (asset.BrowserDownloadUrl, asset.GetSha256Hash());
	}

	/// <summary>
	/// ファイルのSHA256ハッシュを計算
	/// </summary>
	private static async Task<string> ComputeSha256HashAsync(string filePath)
	{
		using var stream = File.OpenRead(filePath);
		var hashBytes = await SHA256.HashDataAsync(stream);
		return Convert.ToHexStringLower(hashBytes);
	}

	/// <summary>
	/// GitHub APIから取得したハッシュを使用してファイルを検証
	/// </summary>
	private async Task<bool> VerifyFileHashAsync(string filePath, string? expectedHash)
	{
		if (string.IsNullOrEmpty(expectedHash))
		{
			Logger.LogWarning("GitHub APIにハッシュ情報がないため、ハッシュ検証をスキップします");
			return true;
		}

		try
		{
			Logger.LogDebug("ダウンロードしたファイルのハッシュを検証します");

			// 実際のファイルハッシュを計算
			var actualHash = await ComputeSha256HashAsync(filePath);

			// GitHub APIのハッシュと比較（大文字小文字を区別しない）
			if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
			{
				Logger.LogError("ハッシュ検証に失敗しました (期待値: {ExpectedHash}, 実際: {ActualHash})", expectedHash, actualHash);
				return false;
			}

			Logger.LogInformation("ハッシュ検証に成功しました");
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "ハッシュ検証中にエラーが発生しました。検証をスキップします");
			return true;
		}
	}

	/// <summary>
	/// ファイルをダウンロード
	/// </summary>
	private async Task<string> DownloadFileAsync(string url, string? expectedHash)
	{
		var tempFile = Path.GetTempFileName();

		IsUpdateIndeterminate = false;
		UpdateProgressMax = 100;

		try
		{
			await using var fileStream = File.OpenWrite(tempFile);
			using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
			var contentLength = response.Content.Headers.ContentLength ?? throw new Exception("ダウンロードサイズが取得できません");

			await using var inputStream = await response.Content.ReadAsStreamAsync();

			var total = 0;
			var buffer = new byte[8192];
			while (true)
			{
				var read = await inputStream.ReadAsync(buffer);
				if (read == 0)
					break;

				total += read;
				UpdateProgress = (double)total / contentLength * 100;
				UpdateState = $"更新ファイルのダウンロード中: {total / 1024:#,0}kb / {contentLength / 1024:#,0}kb";

				await fileStream.WriteAsync(buffer.AsMemory(0, read));
			}
		}
		catch
		{
			// ダウンロード失敗時は一時ファイルを削除
			if (File.Exists(tempFile))
			{
				try
				{
					File.Delete(tempFile);
				}
				catch { }
			}
			throw;
		}

		// ハッシュ検証
		UpdateState = "ダウンロードしたファイルを検証中";
		if (!await VerifyFileHashAsync(tempFile, expectedHash))
		{
			// ハッシュ検証失敗時は一時ファイルを削除
			if (File.Exists(tempFile))
			{
				try
				{
					File.Delete(tempFile);
				}
				catch { }
			}
			throw new Exception("ダウンロードしたファイルのハッシュ検証に失敗しました");
		}

		return tempFile;
	}

	/// <summary>
	/// Windows用の自己更新処理
	/// </summary>
	private async Task UpdateWindows(string newVersionZip)
	{
		Logger.LogInformation("Windows用自己更新を実行します");

		var currentExe = Environment.ProcessPath!;
		var backupExe = currentExe + ".old";
		var tempDir = Path.Combine(Path.GetTempPath(), "kevi-update-" + Guid.NewGuid());

		try
		{
			// ZIPを展開
			Directory.CreateDirectory(tempDir);
			await Task.Run(() => ZipFile.ExtractToDirectory(newVersionZip, tempDir));

			var newExe = Path.Combine(tempDir, "KyoshinEewViewer.exe");
			if (!File.Exists(newExe))
				throw new FileNotFoundException("更新ファイルが見つかりません", newExe);

			// 古いバックアップを削除
			if (File.Exists(backupExe))
				File.Delete(backupExe);

			// 現在の実行ファイルをリネーム（Windows 7+で実行中でもOK）
			File.Move(currentExe, backupExe);

			// 新バージョンを配置
			File.Copy(newExe, currentExe);

			Logger.LogInformation("更新が完了しました。アプリケーションを再起動します");

			// 設定を保存
			ConfigurationLoader.Save(Config);

			await Task.Delay(500);
		}
		finally
		{
			// 一時ファイルをクリーンアップ
			try
			{
				if (File.Exists(newVersionZip))
					File.Delete(newVersionZip);
				if (Directory.Exists(tempDir))
					Directory.Delete(tempDir, true);
			}
			catch { }
		}

		// 再起動
		Process.Start(currentExe);
		(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
	}

	/// <summary>
	/// Linux用の自己更新処理
	/// </summary>
	private async Task UpdateLinux(string newVersionZip)
	{
		Logger.LogInformation("Linux用自己更新を実行します");

		var currentExe = Environment.ProcessPath!;
		var tempDir = Path.Combine(Path.GetTempPath(), "kevi-update-" + Guid.NewGuid());

		try
		{
			// ZIPを展開
			Directory.CreateDirectory(tempDir);
			await Task.Run(() => ZipFile.ExtractToDirectory(newVersionZip, tempDir));

			var newExe = Path.Combine(tempDir, "KyoshinEewViewer");
			if (!File.Exists(newExe))
				throw new FileNotFoundException("更新ファイルが見つかりません", newExe);

			// 実行権限を付与
			var chmod = Process.Start("chmod", $"+x \"{newExe}\"");
			if (chmod != null)
				await chmod.WaitForExitAsync();

			// 古いファイルを削除（iノードは保持される）
			File.Delete(currentExe);

			// 新バージョンを配置（新しいiノードで作成）
			File.Move(newExe, currentExe);

			Logger.LogInformation("更新が完了しました。アプリケーションを再起動します");

			// 設定を保存
			ConfigurationLoader.Save(Config);

			await Task.Delay(500);

		}
		finally
		{
			// 一時ファイルをクリーンアップ
			try
			{
				if (File.Exists(newVersionZip))
					File.Delete(newVersionZip);
				if (Directory.Exists(tempDir))
					Directory.Delete(tempDir, true);
			}
			catch { }
		}

		// 再起動
		Process.Start(currentExe);
		(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
	}

	/// <summary>
	/// macOS用の自己更新処理
	/// </summary>
	private async Task UpdateMacOS(string newVersionZip)
	{
		Logger.LogInformation("macOS用自己更新を実行します");

		// .appバンドルのルートパスを取得 (MacOS/ -> Contents/ -> .app/)
		var appPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../"));

		// .appバンドル内で実行されているか確認
		if (!appPath.EndsWith(".app/", StringComparison.OrdinalIgnoreCase))
		{
			Logger.LogError(".appバンドル内で実行されていません。パス: {AppPath}", appPath);
			UpdateState = ".appバンドル内で実行されていないため、自動更新を実行できません。";
			throw new InvalidOperationException(".appバンドル内で実行されていないため、自動更新を実行できません。");
		}

		// Contents/MacOS/実行ファイルの構造になっているか確認
		var contentsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../"));
		var macOSPath = Path.GetFullPath(AppContext.BaseDirectory);
		if (!contentsPath.EndsWith("Contents/", StringComparison.OrdinalIgnoreCase) ||
			!macOSPath.EndsWith("MacOS/", StringComparison.OrdinalIgnoreCase))
		{
			Logger.LogError("正しい.appバンドル構造ではありません。Contents: {ContentsPath}, MacOS: {MacOSPath}", contentsPath, macOSPath);
			UpdateState = "正しい.appバンドル構造ではないため、自動更新を実行できません。";
			throw new InvalidOperationException("正しい.appバンドル構造ではないため、自動更新を実行できません。");
		}

		Logger.LogInformation(".appバンドルパス: {AppPath}", appPath);

		var tempDir = Path.Combine(Path.GetTempPath(), "kevi-update-" + Guid.NewGuid());

		try
		{
			// ZIPを展開
			Directory.CreateDirectory(tempDir);
			await Task.Run(() => ZipFile.ExtractToDirectory(newVersionZip, tempDir));

			var newAppPath = Path.Combine(tempDir, "KyoshinEewViewer.Desktop.app");
			if (!Directory.Exists(newAppPath))
				throw new DirectoryNotFoundException("更新ファイルが見つかりません");

			// macOSの隔離属性を削除
			Logger.LogInformation("隔離属性を削除します");
			UpdateState = "更新を適用しています";

			try
			{
				// xattrで隔離属性のみ削除
				var xattrProc = Process.Start(new ProcessStartInfo
				{
					FileName = "xattr",
					ArgumentList = { "-r", "-d", "com.apple.quarantine", newAppPath },
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				});

				if (xattrProc != null)
				{
					await xattrProc.WaitForExitAsync();
					if (xattrProc.ExitCode != 0)
					{
						var error = await xattrProc.StandardError.ReadToEndAsync();
						Logger.LogWarning("xattrコマンドが失敗しました（続行します）: {Error}", error);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "xattrコマンドの実行に失敗しました（続行します）");
			}

			// 古い.appバンドルを削除（iノードは保持）
			Directory.Delete(appPath, true);

			// 新しい.appを配置
			Directory.Move(newAppPath, appPath);

			Logger.LogInformation("更新が完了しました。アプリケーションを再起動します");

			// 設定を保存
			ConfigurationLoader.Save(Config);

			await Task.Delay(500);
		}
		finally
		{
			// 一時ファイルをクリーンアップ
			try
			{
				if (File.Exists(newVersionZip))
					File.Delete(newVersionZip);
				if (Directory.Exists(tempDir))
					Directory.Delete(tempDir, true);
			}
			catch { }
		}

		// 現在のプロセス終了を待ってから同じ.appバンドルを起動する
		var appToOpen = appPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		const string relaunchScript = "sleep 0.5; while kill -0 \"$1\" 2>/dev/null; do sleep 0.2; done; /usr/bin/open \"$2\"";
		var openProc = new ProcessStartInfo
		{
			FileName = "/bin/sh",
			ArgumentList = { "-c", relaunchScript, "kevi-relaunch", Environment.ProcessId.ToString(), appToOpen },
			UseShellExecute = false,
			CreateNoWindow = true
		};
		Process.Start(openProc);
		
		await Task.Delay(100);
		(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
	}

	public void StartUpdateCheckTask()
		=> CheckUpdateTask.Change(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(100));

}

public partial class JenkinsBuildInformation
{
	[JsonPropertyName("number")]
	public int Number { get; set; }
}
