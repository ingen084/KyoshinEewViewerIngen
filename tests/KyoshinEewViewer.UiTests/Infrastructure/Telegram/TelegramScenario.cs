using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using DmdataSharp;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.Tsunami;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace KyoshinEewViewer.UiTests.Infrastructure.Telegram;

/// <summary>
/// 本物の電文の受信処理と画面を、模擬した回線につないで組み立てる<br/>
/// 地震情報・津波情報の画面と、緊急地震速報の受信状態を確認できる
/// </summary>
internal sealed class TelegramScenario : IAsyncDisposable
{
	/// <summary>
	/// 起動前から配信されている地震情報
	/// </summary>
	public static readonly TelegramFixtures.EarthquakeTelegram InitialEarthquake = new("fixture-eq-1", "20261018095600", new(2026, 10, 18, 9, 56, 0, TimeSpan.FromHours(9)), "千葉県東方沖");
	private const string DmdataWebSocketSource = "DM-D.S.S(WS)";

	private readonly ServiceProvider _provider;
	private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), "kevi-ui-telegram-" + Guid.NewGuid().ToString("N"));
	private readonly List<Window> _windows = [];
	private int _eewUpdatedCount;

	public ManualTimeProvider Time { get; } = new();
	public SimulatedTelegramNetwork Network { get; } = new();
	public LogRecorder Logs { get; } = new();
	public KyoshinEewViewerConfiguration Config { get; } = new();
	public TelegramProvideService Telegrams { get; }
	public DmdataRedundantTelegramPublisher Dmdata { get; }
	public JmaXmlTelegramPublisher Jma { get; }
	public EarthquakeSeries Earthquake { get; }
	public TsunamiSeries Tsunami { get; }
	public EewTelegramSubscriber Eew { get; }
	public Window EarthquakeWindow { get; }
	public Window TsunamiWindow { get; }

	/// <summary>
	/// 緊急地震速報の表示が更新された回数
	/// </summary>
	public int EewUpdatedCount => Volatile.Read(ref _eewUpdatedCount);

	private TelegramScenario(bool useDmdata, bool useWebSocket)
	{
		Config.NetworkTime.Enable = false;
		Config.Notification.Enable = false;
		Config.Update.Enable = false;
		if (useDmdata)
		{
			Config.Dmdata.OAuthClientId = "fixture-client";
			Config.Dmdata.RefreshToken = "fixture-refresh-token";
			Config.Dmdata.ApiBaseUrl = SimulatedTelegramNetwork.DmdataApiBaseUrl;
			Config.Dmdata.DataApiBaseUrl = SimulatedTelegramNetwork.DmdataDataApiBaseUrl;
			Config.Dmdata.UseWebSocket = useWebSocket;
		}

		var services = new ServiceCollection();
		services.AddSingleton(Config);
		services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(b => b.AddProvider(Logs)));
		services.AddKyoshinEewViewer();
		services.AddSingleton(sp => new InformationCacheService(sp.GetRequiredService<ILogger<InformationCacheService>>(), _cacheDirectory));
		services.AddSingleton(sp => new DmdataRedundantTelegramPublisher(
			sp.GetRequiredService<ILogger<DmdataRedundantTelegramPublisher>>(),
			Config,
			sp.GetRequiredService<InformationCacheService>(),
			DmdataApiClientBuilder.UseOwnHttpClient(new HttpClient(Network, false)),
			client => new FakeOAuthCredential(client),
			Network.CreateSocket,
			Time));
		services.AddSingleton(sp => new JmaXmlTelegramPublisher(
			sp.GetRequiredService<ILogger<JmaXmlTelegramPublisher>>(),
			sp.GetRequiredService<TimerService>(),
			sp.GetRequiredService<InformationCacheService>(),
			new HttpClient(Network, false),
			SimulatedTelegramNetwork.JmaFeedBaseUrl,
			Time));
		_provider = services.BuildServiceProvider();
		ServiceLocator.SetProvider(_provider);

		Telegrams = _provider.GetRequiredService<TelegramProvideService>();
		Dmdata = _provider.GetRequiredService<DmdataRedundantTelegramPublisher>();
		Jma = _provider.GetRequiredService<JmaXmlTelegramPublisher>();

		// 受信開始前に購読しておく必要がある
		Earthquake = _provider.GetRequiredService<EarthquakeSeries>();
		Tsunami = _provider.GetRequiredService<TsunamiSeries>();
		// 強震モニタの画面を使わずに、緊急地震速報の受信処理だけを組み立てる
		var eewController = new EewController(
			_provider.GetRequiredService<ILogger<EewController>>(),
			null!,
			Config,
			_provider.GetRequiredService<SoundPlayerService>(),
			_provider.GetRequiredService<WorkflowService>());
		eewController.EewUpdated += (_, eews) =>
		{
			if (eews.Length > 0)
				Interlocked.Increment(ref _eewUpdatedCount);
		};
		Eew = new EewTelegramSubscriber(
			_provider.GetRequiredService<ILogger<EewTelegramSubscriber>>(),
			eewController,
			Telegrams,
			_provider.GetRequiredService<TimerService>());

		// 失敗時に画面の状態の変化を追えるよう記録する
		Earthquake.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(EarthquakeSeries.IsLoading) or nameof(EarthquakeSeries.IsFault) or nameof(EarthquakeSeries.SourceString))
				Logs.Add($"地震情報 {e.PropertyName}: 取得中={Earthquake.IsLoading} 障害={Earthquake.IsFault} 取得元={Earthquake.SourceString}");
		};
		Eew.PropertyChanged += (_, e) => Logs.Add($"緊急地震速報 {e.PropertyName}: 有効={Eew.Enabled} 切断={Eew.IsDisconnected}");

		EarthquakeWindow = Show(new EarthquakeView { DataContext = Earthquake });
		TsunamiWindow = Show(new TsunamiView { DataContext = Tsunami });
	}

	/// <summary>
	/// 回線とdmdataの状態を指定して受信を開始する
	/// </summary>
	/// <param name="useWebSocket">dmdata の WebSocket を使うか。使わない場合はPULL型で受信する</param>
	public static async Task<TelegramScenario> StartAsync(bool useDmdata = true, bool useWebSocket = true, bool online = true, bool dmdataAvailable = true, bool webSocketAvailable = true)
	{
		var scenario = new TelegramScenario(useDmdata, useWebSocket);
		scenario.Network.Online = online;
		scenario.Network.DmdataAvailable = dmdataAvailable;
		scenario.Network.WebSocketAvailable = webSocketAvailable;
		scenario.Network.Publish(InitialEarthquake);
		await scenario.Telegrams.StartAsync();
		return scenario;
	}

	/// <summary>
	/// dmdata の設定画面を表示する
	/// </summary>
	public Window ShowDmdataSettings()
		// 操作中にスクロールしないよう、設定画面全体が収まる大きさにする
		=> Show(_provider.GetRequiredService<DmdataSettingPage>().DisplayControl, height: 4000);

	public Window Show(Control content, double width = 1000, double height = 700)
	{
		var window = new Window { Width = width, Height = height, Content = content };
		window.Show();
		Dispatcher.UIThread.RunJobs();
		_windows.Add(window);
		return window;
	}

	/// <summary>
	/// 時刻を進め、再接続や復旧確認のタイマーを発火させる
	/// </summary>
	public void Advance(TimeSpan duration)
	{
		Time.Advance(duration);
		Dispatcher.UIThread.RunJobs();
	}

	/// <summary>
	/// 非同期の処理を進めながら、アプリの時刻を1秒ずつ進める
	/// </summary>
	public async Task AdvanceAsync(TimeSpan duration)
	{
		for (var advanced = TimeSpan.Zero; advanced < duration; advanced += TimeSpan.FromSeconds(1))
		{
			Advance(TimeSpan.FromSeconds(1));
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
	}

	/// <summary>
	/// 時刻を進めずに、受信処理や画面の更新が落ち着くまで少し待つ<br/>
	/// 状態が変わらないことを確認する前に使う
	/// </summary>
	public async Task SettleAsync()
	{
		for (var i = 0; i < 30; i++)
		{
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
	}

	/// <summary>
	/// dmdata の PULL型の定期取得を指定した回数試みるまで、時刻を進めながら待つ
	/// </summary>
	public Task WaitPullAttemptsAsync(int count)
	{
		const string telegramList = SimulatedTelegramNetwork.DmdataApiBaseUrl + "/v2/telegram?";
		var target = Network.RequestCount(telegramList) + count;
		return WaitAsync(
			() => Network.RequestCount(telegramList) >= target,
			() => $"PULL型で{count}回取得を試みる",
			TimeSpan.FromSeconds(30));
	}

	/// <summary>
	/// 防災情報XMLの定期取得 (毎分) を実行する
	/// </summary>
	public async Task PollJmaAsync()
	{
		Advance(TimeSpan.FromMinutes(1));
		await Jma.PollAsync();
	}

	/// <summary>
	/// 地震情報の取得元を待つ
	/// </summary>
	/// <param name="advance">待つ間に進めるアプリの時刻の上限</param>
	public Task WaitEarthquakeSourceAsync(string source, TimeSpan advance = default)
		=> WaitAsync(
			() => !Earthquake.IsLoading && !Earthquake.IsFault && Earthquake.SourceString == source && Ui.VisibleTexts(EarthquakeWindow).Contains("取得元: " + source),
			() => $"地震情報を {source} から受信する (現在: {Earthquake.SourceString}, 障害: {Earthquake.IsFault}, 取得中: {Earthquake.IsLoading})",
			advance);

	/// <summary>
	/// dmdata の WebSocket で、地震情報・津波情報・緊急地震速報の受信を始め終えるのを待つ<br/>
	/// 受信開始の処理では情報ごとに過去の電文を取得するため、回線などを操作するテストは最後の情報まで待ってから操作する
	/// </summary>
	public async Task WaitDmdataWebSocketAsync()
	{
		await WaitEarthquakeSourceAsync(DmdataWebSocketSource);
		await Ui.WaitUntilAsync(() => Tsunami.SourceName == DmdataWebSocketSource, () => $"津波情報を dmdata から受信する\n{Logs}");
		await WaitEewReceivingAsync();
		await SettleAsync();
	}

	/// <summary>
	/// 地震情報・津波情報の画面に障害の表示と再試行ボタンが出るのを待つ
	/// </summary>
	/// <param name="advance">待つ間に進めるアプリの時刻の上限</param>
	public Task WaitFaultAsync(TimeSpan advance = default)
		=> WaitAsync(
			() => new[] { EarthquakeWindow, TsunamiWindow }.All(w => Ui.VisibleTexts(w).Contains("情報の取得に問題が発生しました") && Ui.FindButton(w, "再試行").IsEffectivelyVisible),
			() => $"地震情報と津波情報に障害が表示される (地震情報: {Earthquake.IsFault}, 津波情報: {Tsunami.IsFault})",
			advance);

	/// <summary>
	/// 緊急地震速報を dmdata から受信できている状態を待つ
	/// </summary>
	/// <param name="advance">待つ間に進めるアプリの時刻の上限</param>
	public Task WaitEewReceivingAsync(TimeSpan advance = default)
		=> WaitAsync(
			() => Eew.Enabled && !Eew.IsDisconnected && Network.Socket is { IsConnected: true, Disposed: false },
			() => $"緊急地震速報を受信できる (有効: {Eew.Enabled}, 切断: {Eew.IsDisconnected})",
			advance);

	/// <summary>
	/// 条件を満たすまで待つ<br/>
	/// 再接続や復旧確認のタイマーは非同期の処理のあとに予約されるため、時刻をまとめて進めると予約より前に通り過ぎることがある。
	/// 時刻を進める場合は1秒ずつ進めながら待つ
	/// </summary>
	public async Task WaitAsync(Func<bool> condition, Func<string> describe, TimeSpan advance)
	{
		var advanced = TimeSpan.Zero;
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (true)
		{
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
			if (condition())
				return;
			if (DateTime.UtcNow >= deadline)
				throw new XunitException($"アプリの時刻を{advanced.TotalSeconds}秒進めても条件を満たしませんでした: {describe()}\n{Logs}");
			if (advanced < advance)
			{
				Advance(TimeSpan.FromSeconds(1));
				advanced += TimeSpan.FromSeconds(1);
			}
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
	}

	/// <summary>
	/// 緊急地震速報を配信し、画面の更新まで届くことを確認する
	/// </summary>
	public async Task AssertEewDeliveredAsync(string eventId)
	{
		var before = EewUpdatedCount;
		Network.Socket!.Receive(TelegramFixtures.DmdataEewMessage("fixture-eew-" + eventId, eventId, 1, DateTimeOffset.Now.AddSeconds(-10), "千葉県東方沖"));
		await Ui.WaitUntilAsync(() => EewUpdatedCount > before, () => $"緊急地震速報 {eventId} が届く\n{Logs}");
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var window in _windows)
			window.Close();
		// テストの終了後に定期取得で通信しないよう、受信を止める
		Jma.Stop([InformationCategory.Earthquake, InformationCategory.Tsunami, InformationCategory.Typhoon]);
		await _provider.DisposeAsync();
		Dispatcher.UIThread.RunJobs();
		TestContext.Current.TestOutputHelper?.WriteLine(Logs.ToString());
		try
		{
			Directory.Delete(_cacheDirectory, true);
		}
		catch (DirectoryNotFoundException) { }
	}
}
