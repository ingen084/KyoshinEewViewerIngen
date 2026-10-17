using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.UiTests.Infrastructure;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using KyoshinEewViewer.Views.SettingPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.UiTests;

// Linuxの自己更新処理 (実行権限の付与と、終了後に置き換えて起動するスクリプトを含む) を実際に動かす
[SupportedOSPlatform("linux")]
public sealed class UpdateUiTests
{
	private static readonly Uri ReleasesUri = new("https://fixture.invalid/repos/kevi/releases");
	// UIテストはLinuxで実行するため、Linux向けのアセットを配信する
	private static readonly string AssetName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
		? "KyoshinEewViewer-ubuntu-arm64.zip"
		: "KyoshinEewViewer-ubuntu-x64.zip";
	private static readonly Uri AssetUri = new($"https://fixture.invalid/download/{AssetName}");
	private const string ReleaseNote = "## 主な変更点\n\n- 緊急地震速報の表示を改善しました\n- 地震情報の更新が遅れる問題を修正しました";
	private static readonly byte[] OldExecutable = Encoding.UTF8.GetBytes("#!/bin/sh\n# KyoshinEewViewer 1.0.0\n");
	// 起動されたときのパスを書き出し、置き換え後に起動されたことを確認できるようにする
	private static readonly byte[] NewExecutable = Encoding.UTF8.GetBytes("#!/bin/sh\n# KyoshinEewViewer 99.0.0\necho \"$0\" > \"$0.launched\"\n");

	[AvaloniaFact]
	public async Task 更新があると設定画面に更新内容が表示され_適用すると終了後に実行ファイルを置き換えて再起動する()
	{
		var package = CreatePackage();
		await using var app = new UpdateScenario(package, Sha256(package));
		await app.ShowSettingsWithUpdateAsync();

		Ui.Click(app.Window, Ui.FindButton(app.Window, "更新を適用する"));
		await Ui.WaitUntilAsync(() => app.ShutdownRequested.Task.IsCompleted, "アプリケーションを終了する", 15);
		var texts = Ui.VisibleTexts(app.Window);
		Assert.Contains("更新状況", texts);
		Assert.Contains("更新を適用しています", texts);

		// 実行中の旧バージョンが自身を読み込めるよう、終了するまでは置き換えない
		await Task.Delay(500);
		Assert.Equal(OldExecutable, File.ReadAllBytes(app.ExecutablePath));
		Assert.False(File.Exists(app.LaunchedPath));

		app.ExitRunningApplication();
		await Ui.WaitUntilAsync(
			() => File.Exists(app.LaunchedPath) && File.ReadAllText(app.LaunchedPath).Length > 0,
			"置き換えた実行ファイルが起動される", 15);

		Assert.Equal(app.ExecutablePath + "\n", File.ReadAllText(app.LaunchedPath));
		Assert.Equal(NewExecutable, File.ReadAllBytes(app.ExecutablePath));
		Assert.True(File.GetUnixFileMode(app.ExecutablePath).HasFlag(UnixFileMode.UserExecute), "置き換えた実行ファイルに実行権限がありません。");
	}

	[AvaloniaFact]
	public async Task 更新ファイルが配信元のハッシュと一致しなければ置き換えずに失敗を表示し_再試行できる()
	{
		await using var app = new UpdateScenario(CreatePackage(), Sha256(OldExecutable));
		await app.ShowSettingsWithUpdateAsync();

		var apply = Ui.FindButton(app.Window, "更新を適用する");
		Ui.Click(app.Window, apply);
		await Ui.WaitUntilAsync(
			() => Ui.VisibleTexts(app.Window).Contains("更新に失敗しました。繰り返し失敗する場合は手動での更新をお願いします。"),
			"更新の失敗が表示される", 15);
		await Ui.WaitUntilAsync(() => apply.IsEffectivelyEnabled, "更新を再試行できる");

		Assert.False(app.ShutdownRequested.Task.IsCompleted);
		Assert.Equal(OldExecutable, File.ReadAllBytes(app.ExecutablePath));
	}

	/// <summary>
	/// 本物の更新確認サービスと設定画面を、テスト用の通信先と一時ディレクトリの実行ファイルで組み立てる
	/// </summary>
	private sealed class UpdateScenario : IAsyncDisposable
	{
		private readonly string? _previousVersion = Utils.OverrideVersion;
		private readonly string _directory = Path.Combine(Path.GetTempPath(), "kevi-ui-update-" + Guid.NewGuid().ToString("N"));
		private readonly Process _runningApplication;
		private readonly ServiceProvider _provider;
		private readonly UpdateCheckService _service;

		public string ExecutablePath { get; }
		public string LaunchedPath => ExecutablePath + ".launched";
		public TaskCompletionSource ShutdownRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public SettingWindow Window { get; }

		public UpdateScenario(byte[] package, string digest)
		{
			Utils.OverrideVersion = "1.0.0";
			// HOME 配下はWSLでWindowsのファイルシステムになることがあり、実行権限を正しく扱えないため一時ディレクトリを使う
			Directory.CreateDirectory(_directory);
			ExecutablePath = Path.Combine(_directory, "KyoshinEewViewer");
			File.WriteAllBytes(ExecutablePath, OldExecutable);
			File.SetUnixFileMode(ExecutablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

			var http = new FixtureHttpHandler();
			var releases = CreateReleasesJson(package.Length, digest);
			// 更新の確認時と適用時にそれぞれリリース一覧を取得する
			http.Register(HttpMethod.Get, ReleasesUri, releases);
			http.Register(HttpMethod.Get, ReleasesUri, releases);
			http.Register(HttpMethod.Get, AssetUri, package);

			var config = new KyoshinEewViewerConfiguration();
			// 定期確認のタイマーからは通信させず、テストから確認を実行する
			config.Update.Enable = false;

			// 更新を適用する旧バージョンのプロセスの代わりに、テストから終了させられるプロセスを使う
			_runningApplication = Process.Start("sleep", "infinity");

			var services = new ServiceCollection();
			services.AddSingleton(config);
			services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(_ => { }));
			services.AddKyoshinEewViewer();
			services.AddSingleton(sp => new UpdateCheckService(
				sp.GetRequiredService<ILogger<UpdateCheckService>>(),
				config,
				sp.GetRequiredService<WorkflowService>(),
				new HttpClient(http),
				ReleasesUri.ToString(),
				null,
				ExecutablePath,
				_runningApplication.Id,
				() => ShutdownRequested.TrySetResult()));
			_provider = services.BuildServiceProvider();
			// 設定画面は閉じるときに DI コンテナから設定を取得して保存する
			ServiceLocator.SetProvider(_provider);
			_service = _provider.GetRequiredService<UpdateCheckService>();
			Window = new SettingWindow { DataContext = _provider.GetRequiredService<SettingWindowViewModel>() };
		}

		public async Task ShowSettingsWithUpdateAsync()
		{
			Window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.Empty(Window.GetVisualDescendants().OfType<UpdatePage>());

			// 本番では定期確認のタイマーから呼ばれるため、UIスレッドの外で実行する
			await Task.Run(_service.CheckUpdateAsync);
			await Ui.WaitUntilAsync(
				() => Ui.VisibleTexts(Window).Contains("緊急地震速報の表示を改善しました"),
				"更新ページにリリースノートが表示される");

			var texts = Ui.VisibleTexts(Window);
			Assert.Contains("アプリケーションの更新があります。", texts);
			Assert.Contains(" v99.0.0.0", texts);
			Assert.Contains(texts, t => t.StartsWith("2026/10/01", StringComparison.Ordinal));
			Assert.Contains("主な変更点", texts);
		}

		/// <summary>
		/// 旧バージョンのプロセスを終了させる
		/// </summary>
		public void ExitRunningApplication()
		{
			_runningApplication.Kill();
			_runningApplication.WaitForExit();
		}

		public async ValueTask DisposeAsync()
		{
			Window.Close();
			Dispatcher.UIThread.RunJobs();
			if (!_runningApplication.HasExited)
				ExitRunningApplication();
			_runningApplication.Dispose();
			await _provider.DisposeAsync();
			Utils.OverrideVersion = _previousVersion;
			Directory.Delete(_directory, true);
		}
	}

	// GitHub Releases API と同じ形式 (tests/IntegrationTest/run-update-test.ps1 と揃える)
	private static string CreateReleasesJson(int size, string digest) => JsonSerializer.Serialize(new[]
	{
		new
		{
			name = "v99.0.0",
			tag_name = "99.0.0",
			body = ReleaseNote,
			draft = false,
			prerelease = false,
			created_at = "2026-10-01T03:00:00Z",
			published_at = "2026-10-01T03:00:00Z",
			assets = new[]
			{
				new
				{
					name = AssetName,
					content_type = "application/zip",
					state = "uploaded",
					size,
					browser_download_url = AssetUri.ToString(),
					digest = "sha256:" + digest,
				},
			},
		},
	});

	private static byte[] CreatePackage()
	{
		using var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
		using (var entry = zip.CreateEntry("KyoshinEewViewer").Open())
			entry.Write(NewExecutable);
		return stream.ToArray();
	}

	private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
