using Avalonia.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Updater;

public partial class MainWindow : Window
{
	private HttpClient Client { get; } = new();
	private const string GithubReleasesUrl = "https://api.github.com/repos/ingen084/KyoshinEewViewerIngen/releases";
	private string UpdateDirectory { get; set; } = "../";
	private const string SettingsFileName = "config.json";

	// RIDとファイルを紐付ける
	private static Dictionary<string, string> RiMap { get; } = new()
	{
		{ "win-x64", "KyoshinEewViewer-windows-x64.zip" },
		{ "win-arm64", "KyoshinEewViewer-windows-arm64.zip" },
		{ "win10-x64", "KyoshinEewViewer-windows-x64.zip" },
		{ "linux-x64", "KyoshinEewViewer-ubuntu-x64.zip" },
		{ "linux-arm64", "KyoshinEewViewer-ubuntu-arm64.zip" },
	};

	public MainWindow()
	{
		InitializeComponent();

		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEViUpdater;" + Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
		CloseButton.Tapped += (s, e) => Close();
		DoUpdate();
	}

	private async void DoUpdate()
	{
		Progress.Value = 0;
		Progress.IsIndeterminate = true;
		ProgressText.Text = "";

		if (Design.IsDesignMode)
			return;

		// 引数で指定されていたら上書きする
		UpdateDirectory = Program.OverrideKevPath ?? UpdateDirectory;

		IDisposable? sentry = null;
		try
		{
			while (Process.GetProcessesByName("KyoshinEewViewer").Length != 0)
			{
				CloseButton.IsEnabled = true;
				InfoText.Text = "KyoshinEewViewer のプロセスが終了するのを待っています";
				await Task.Delay(1000);
			}
			CloseButton.IsEnabled = false;
			InfoText.Text = "適用可能な更新を取得しています";

			// アプリによる保存を待ってから
			await Task.Delay(1000);
			if (!File.Exists(Path.Combine("../", SettingsFileName)))
				throw new Exception("KyoshinEewViewerが見つかりません");
			if (JsonSerializer.Deserialize(File.ReadAllText(Path.Combine("../", SettingsFileName)), KyoshinEewViewerSerializerContext.Default.KyoshinEewViewerConfiguration) is not { } config)
				throw new Exception("KyoshinEewViewerの設定ファイルを読み込むことができません");

			// 取得してでかい順に並べる
			var version = (await GitHubRelease.GetReleasesAsync(Client, GithubReleasesUrl))
				// ドラフトリリースではなく、現在のバージョンより新しく、不安定版が有効
				.Where(r =>
					!r.Draft && (config.Update.UsePreReleaseBuild || !r.Prerelease) &&
					Version.TryParse(r.TagName, out var v) && v > config.SavedVersion &&
					(config.Update.UseUnstableBuild || v.Build == 0))
				.OrderByDescending(r => Version.TryParse(r.TagName, out var v) ? v : new Version())
				.FirstOrDefault();

			if (string.IsNullOrWhiteSpace(version?.Url))
			{
				InfoText.Text = "適用可能な更新はありません";
				Progress.IsIndeterminate = false;
				CloseButton.IsEnabled = true;
				return;
			}

			// エラー情報の収集が有効な場合だけSentryを初期化する
			if (config.Update.SendCrashReport)
			{
				sentry = SentrySdk.Init(o =>
				{
					o.Dsn = "https://5bb942b42f6f4c63ab50a1d429ff69bf@sentry.ingen084.net/3";
					o.AutoSessionTracking = true;
				});
				SentrySdk.ConfigureScope(s =>
				{
					s.Release = Core.Utils.Version;
					s.User = new SentryUser {
						IpAddress = "{{auto}}",
					};
				});
			}

			InfoText.Text = $"v{version.TagName} に更新を行います";

			var ri = RuntimeInformation.RuntimeIdentifier;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				ri = "linux-x64";
			if (!RiMap.TryGetValue(ri, out var value))
			{
				InfoText.Text = "現在のプラットフォームで自動更新は利用できません";
				Progress.IsIndeterminate = false;
				CloseButton.IsEnabled = true;
				return;
			}
			var asset = version.Assets.FirstOrDefault(a => a.Name == value);
			if (asset is null)
			{
				InfoText.Text = "リリース内にファイルが見つかりませんでした";
				Progress.IsIndeterminate = false;
				CloseButton.IsEnabled = true;
				return;
			}

			InfoText.Text = $"v{version.TagName} をダウンロードしています";
			Progress.IsIndeterminate = false;

			var tmpFileName = Path.GetTempFileName();
			// ダウンロード開始
			await using (var fileStream = File.OpenWrite(tmpFileName))
			{
				using var response = await Client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
				Progress.Maximum = 100;
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
					Progress.Value = (double)total / contentLength * 100;
					ProgressText.Text = $"ダウンロード中: {total / 1024:#,0}kb / {contentLength / 1024:#,0}kb";

					await fileStream.WriteAsync(buffer.AsMemory(0, readed));
				}
			}

			InfoText.Text = "ファイルを展開しています";
			Progress.IsIndeterminate = true;
			ProgressText.Text = "";

			await Task.Run(() => ZipFile.ExtractToDirectory(tmpFileName, UpdateDirectory, true));
			File.Delete(tmpFileName);
#if LINUX
			new Mono.Unix.UnixFileInfo(Path.Combine(UpdateDirectory, "KyoshinEewViewer")).FileAccessPermissions |=
					Mono.Unix.FileAccessPermissions.UserExecute | Mono.Unix.FileAccessPermissions.GroupExecute | Mono.Unix.FileAccessPermissions.OtherExecute;
#endif

			InfoText.Text = "更新が完了しました アプリケーションを起動しています";
			Progress.IsIndeterminate = false;

			await Task.Delay(100);

			await Task.Run(() => Process.Start(new ProcessStartInfo(Path.Combine(UpdateDirectory, "KyoshinEewViewer")) { WorkingDirectory = UpdateDirectory }));

			await Task.Delay(2000);

			Close();
		}
		catch (Exception ex)
		{
			SentrySdk.CaptureException(ex);
			InfoText.Text = "更新中に問題が発生しました";
			ProgressText.Text = ex.Message;
			Progress.IsIndeterminate = false;
			CloseButton.IsEnabled = true;
		}
		finally
		{
			sentry?.Dispose();
		}
	}
}
