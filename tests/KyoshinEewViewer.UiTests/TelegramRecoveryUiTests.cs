using Avalonia.Headless.XUnit;
using KyoshinEewViewer.UiTests.Infrastructure;
using KyoshinEewViewer.UiTests.Infrastructure.Telegram;

namespace KyoshinEewViewer.UiTests;

/// <summary>
/// 回線断・dmdataの障害から、電文の受信が復旧することを確認する<br/>
/// 電文の受信処理と画面は本物を使い、回線とdmdataのWebSocketだけを模擬する
/// </summary>
public sealed class TelegramRecoveryUiTests
{
	private const string DmdataWebSocket = "DM-D.S.S(WS)";
	private const string DmdataPull = "DM-D.S.S(PULL)";
	private const string JmaXml = "防災情報XML";
	// 回線やdmdataが復旧してから自動で受信を再開するまでの、アプリの時刻での上限
	private static readonly TimeSpan RecoveryLimit = TimeSpan.FromSeconds(60);
	// 防災情報XMLのフィードの取得。受信元を決めるときの確認 (HEAD) は含めない
	private const string JmaFeedFetch = "GET " + SimulatedTelegramNetwork.JmaFeedBaseUrl;

	[AvaloniaFact]
	public async Task 起動時から回線が切れていると障害を表示し_回線の復旧後に再試行で緊急地震速報を含めて受信できる()
	{
		await using var app = await TelegramScenario.StartAsync(online: false);
		await app.WaitFaultAsync();
		Assert.False(app.Eew.Enabled);

		// 回線が切れたままの再試行では、再び障害を表示して再試行できる
		var requests = app.Network.RequestCount("/v2/contract");
		Ui.Click(app.EarthquakeWindow, Ui.FindButton(app.EarthquakeWindow, "再試行"));
		await app.WaitFaultAsync();
		Assert.True(app.Network.RequestCount("/v2/contract") > requests, "再試行で受信を試みていません。");

		app.Network.Online = true;
		Ui.Click(app.TsunamiWindow, Ui.FindButton(app.TsunamiWindow, "再試行"));
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket);
		await Ui.WaitUntilAsync(() => app.Tsunami.SourceName == DmdataWebSocket && !app.Tsunami.IsFault, () => $"津波情報を dmdata から受信する\n{app.Logs}");
		await app.WaitEewReceivingAsync();
		await app.AssertEewDeliveredAsync("20261018100000");
	}

	[AvaloniaFact]
	public async Task 起動時から回線が切れていても_回線が復旧すると操作しなくても受信を再開する()
	{
		await using var app = await TelegramScenario.StartAsync(online: false);
		await app.WaitFaultAsync();

		app.Network.Online = true;
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket, advance: RecoveryLimit);
		await app.WaitEewReceivingAsync();
		Assert.Contains(TelegramScenario.InitialEarthquake.Place, Ui.VisibleTexts(app.EarthquakeWindow));
	}

	[AvaloniaFact]
	public async Task 防災情報XMLの定期取得に連続3回失敗すると障害を表示し_回線の復旧後は自動または再試行で受信を再開する()
	{
		await using var app = await TelegramScenario.StartAsync(useDmdata: false);
		await app.WaitEarthquakeSourceAsync(JmaXml);
		// dmdata を使わない場合、緊急地震速報は受信しない
		Assert.False(app.Eew.Enabled);

		// 2回までの失敗では表示したまま受信を続け、取得できれば失敗の回数を数え直す
		app.Network.Online = false;
		await PollJmaWithoutFaultAsync(app, 2);
		app.Network.Online = true;
		await PollJmaWithoutFaultAsync(app, 1);
		app.Network.Online = false;
		await PollJmaWithoutFaultAsync(app, 2);

		// 3回続けて失敗すると障害を表示する
		await app.PollJmaAsync();
		await app.WaitFaultAsync();

		// 回線が戻ると自動で受信を再開する
		app.Network.Online = true;
		await app.WaitEarthquakeSourceAsync(JmaXml, advance: RecoveryLimit);
		await Ui.WaitUntilAsync(() => app.Tsunami.SourceName == JmaXml && !app.Tsunami.IsFault, () => $"津波情報を防災情報XMLから受信する\n{app.Logs}");

		// 再試行ボタンでは、自動の確認を待たずにすぐ受信を再開する
		app.Network.Online = false;
		for (var i = 0; i < 3; i++)
			await app.PollJmaAsync();
		await app.WaitFaultAsync();
		app.Network.Online = true;
		Ui.Click(app.EarthquakeWindow, Ui.FindButton(app.EarthquakeWindow, "再試行"));
		await app.WaitEarthquakeSourceAsync(JmaXml);
	}

	/// <summary>
	/// 防災情報XMLの定期取得を行い、障害を表示せずに受信を続けていることを確認する
	/// </summary>
	private static async Task PollJmaWithoutFaultAsync(TelegramScenario app, int count)
	{
		for (var i = 0; i < count; i++)
		{
			await app.PollJmaAsync();
			await app.SettleAsync();
			Assert.False(app.Earthquake.IsFault || app.Tsunami.IsFault, $"定期取得の一時的な失敗で障害を表示しました。\n{app.Logs}");
			Assert.Equal(JmaXml, app.Earthquake.SourceString);
			Assert.Contains(TelegramScenario.InitialEarthquake.Place, Ui.VisibleTexts(app.EarthquakeWindow));
		}
	}

	[AvaloniaFact]
	public async Task dmdataの受信中に回線が切れると緊急地震速報の切断と障害を表示し_回線の復旧後に緊急地震速報を含めて自動で受信を再開する()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitDmdataWebSocketAsync();

		app.Network.Online = false;
		app.Network.Socket!.Drop();
		// 緊急地震速報はWebSocketでしか受信できないため、すぐに切断を表示する
		await Ui.WaitUntilAsync(() => app.Eew.IsDisconnected, () => $"緊急地震速報の切断を表示する\n{app.Logs}");

		// 再接続を試みても回線が切れているため、他の情報源も含めて受信できないことを表示する
		await app.WaitFaultAsync(advance: RecoveryLimit);
		Assert.False(app.Eew.Enabled);

		app.Network.Online = true;
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket, advance: RecoveryLimit);
		await app.WaitEewReceivingAsync();
		await app.AssertEewDeliveredAsync("20261018100500");
	}

	[AvaloniaFact]
	public async Task dmdataが障害になると地震情報と津波情報を防災情報XMLから受信し_dmdataの復旧後に緊急地震速報を含めて自動で戻る()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitDmdataWebSocketAsync();

		app.Network.DmdataAvailable = false;
		app.Network.Socket!.Drop();

		// 地震情報と津波情報は防災情報XMLへ切り替わり、緊急地震速報は代わりの情報源がない
		await app.WaitEarthquakeSourceAsync(JmaXml, advance: RecoveryLimit);
		await Ui.WaitUntilAsync(() => app.Tsunami.SourceName == JmaXml, () => $"津波情報を防災情報XMLから受信する\n{app.Logs}");
		Assert.False(app.Eew.Enabled);

		app.Network.DmdataAvailable = true;
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket, advance: RecoveryLimit);
		await Ui.WaitUntilAsync(() => app.Tsunami.SourceName == DmdataWebSocket, () => $"津波情報を dmdata から受信する\n{app.Logs}");
		await app.WaitEewReceivingAsync();
		await app.AssertEewDeliveredAsync("20261018101000");

		// dmdata に戻ったあとは防災情報XMLを取得しない
		var jmaRequests = app.Network.RequestCount(SimulatedTelegramNetwork.JmaFeedBaseUrl);
		await app.PollJmaAsync();
		Assert.Equal(jmaRequests, app.Network.RequestCount(SimulatedTelegramNetwork.JmaFeedBaseUrl));
	}

	[AvaloniaFact]
	public async Task 起動時にdmdataが障害でも防災情報XMLで受信を始め_dmdataの復旧後に緊急地震速報を含めて自動で切り替わる()
	{
		await using var app = await TelegramScenario.StartAsync(dmdataAvailable: false);
		await app.WaitEarthquakeSourceAsync(JmaXml);
		Assert.False(app.Eew.Enabled);

		app.Network.DmdataAvailable = true;
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket, advance: RecoveryLimit);
		await app.WaitEewReceivingAsync();
		await app.AssertEewDeliveredAsync("20261018101500");
	}

	[AvaloniaFact]
	public async Task WebSocketに接続できない場合はPULL型で地震情報を受信して緊急地震速報の切断を表示し_接続できるようになるとWebSocketに戻る()
	{
		await using var app = await TelegramScenario.StartAsync(webSocketAvailable: false);
		await app.WaitEarthquakeSourceAsync(DmdataPull);
		await Ui.WaitUntilAsync(() => app.Eew.IsDisconnected, () => $"緊急地震速報の切断を表示する\n{app.Logs}");

		app.Network.WebSocketAvailable = true;
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket, advance: RecoveryLimit);
		await app.WaitEewReceivingAsync();
		await app.AssertEewDeliveredAsync("20261018101700");
	}

	[AvaloniaFact]
	public async Task WebSocketだけが切断されると緊急地震速報の切断を表示して自動再接続を待ち_再接続後に切断中の地震情報を取得し直す()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitDmdataWebSocketAsync();

		var socket = app.Network.Socket!;
		socket.Drop();
		await Ui.WaitUntilAsync(() => app.Eew.IsDisconnected, () => $"緊急地震速報の切断を表示する\n{app.Logs}");

		// 30秒間は DmdataSharp による自動再接続に任せ、接続し直さない。地震情報は表示したまま待つ
		await app.AdvanceAsync(TimeSpan.FromSeconds(25));
		Assert.Same(socket, app.Network.Socket);
		Assert.False(socket.Disposed, $"自動再接続を待たずに接続し直しました。\n{app.Logs}");
		Assert.False(app.Earthquake.IsFault);
		Assert.Contains(TelegramScenario.InitialEarthquake.Place, Ui.VisibleTexts(app.EarthquakeWindow));

		// 切断中に発表された地震情報は、DmdataSharp による再接続後に取得し直す
		var missed = new TelegramFixtures.EarthquakeTelegram("fixture-eq-2", "20261018101800", new(2026, 10, 18, 10, 18, 0, TimeSpan.FromHours(9)), "茨城県南部");
		app.Network.Publish(missed);
		socket.Reconnect();

		await Ui.WaitUntilAsync(
			() => !app.Eew.IsDisconnected && Ui.VisibleTexts(app.EarthquakeWindow).Contains(missed.Place),
			() => $"切断中の地震情報を表示する\n{app.Logs}");
		await app.AssertEewDeliveredAsync("20261018102000");
	}

	[AvaloniaFact]
	public async Task WebSocketの再接続後に切断中の地震情報を取得できなくても_取得できるようになると取得し直す()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitDmdataWebSocketAsync();

		var socket = app.Network.Socket!;
		socket.Drop();
		var missed = new TelegramFixtures.EarthquakeTelegram("fixture-eq-3", "20261018103000", new(2026, 10, 18, 10, 30, 0, TimeSpan.FromHours(9)), "茨城県南部");
		app.Network.Publish(missed);

		// WebSocketには再接続できたが、電文を取得するAPIが一時的に応答しない
		app.Network.DmdataAvailable = false;
		socket.Reconnect();
		await app.WaitEewReceivingAsync();
		await app.SettleAsync();
		Assert.DoesNotContain(missed.Place, Ui.VisibleTexts(app.EarthquakeWindow));
		Assert.False(app.Earthquake.IsFault);

		app.Network.DmdataAvailable = true;
		await app.WaitAsync(
			() => Ui.VisibleTexts(app.EarthquakeWindow).Contains(missed.Place),
			() => "切断中の地震情報を取得し直して表示する",
			TimeSpan.FromSeconds(15));
		Assert.Equal(DmdataWebSocket, app.Earthquake.SourceString);
		await app.AssertEewDeliveredAsync("20261018103100");
	}

	[AvaloniaFact]
	public async Task 受信中に契約情報を一時的に取得できなくても_受信元を切り替えない()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitDmdataWebSocketAsync();
		var jmaRequests = app.Network.RequestCount(JmaFeedFetch);
		var contractRequests = app.Network.RequestCount("/v2/contract");

		// 接続し直すと、受信元を決め直すために契約情報を取得する
		app.Network.ContractListAvailable = false;
		app.Network.Socket!.Drop();
		await app.Dmdata.ReconnectImmediatelyAsync();
		await app.WaitEewReceivingAsync();
		await Ui.WaitUntilAsync(() => app.Network.RequestCount("/v2/contract") > contractRequests, () => $"契約情報を取得し直す\n{app.Logs}");
		await app.SettleAsync();

		Assert.False(app.Earthquake.IsFault || app.Tsunami.IsFault, $"契約情報の一時的なエラーで障害を表示しました。\n{app.Logs}");
		Assert.Equal(DmdataWebSocket, app.Earthquake.SourceString);
		Assert.Equal(DmdataWebSocket, app.Tsunami.SourceName);
		Assert.Equal(jmaRequests, app.Network.RequestCount(JmaFeedFetch));
		await app.AssertEewDeliveredAsync("20261018103500");
	}

	[AvaloniaFact]
	public async Task PULL型の定期取得に失敗しても連続3回までは受信を続け_それを超えると防災情報XMLに切り替える()
	{
		await using var app = await TelegramScenario.StartAsync(useWebSocket: false);
		await app.WaitEarthquakeSourceAsync(DmdataPull);
		var jmaRequests = app.Network.RequestCount(JmaFeedFetch);
		// 失敗時は前回の取得で指定された間隔で取得し直すため、通常の間隔で1回取得しておく
		await app.WaitPullAttemptsAsync(1);

		// 2回続けて失敗しても切り替えない
		app.Network.DmdataAvailable = false;
		await app.WaitPullAttemptsAsync(2);
		await AssertPullContinuesAsync();

		// 取得できれば失敗の回数を数え直す。失敗中に発表された地震情報も受信する
		app.Network.DmdataAvailable = true;
		var next = new TelegramFixtures.EarthquakeTelegram("fixture-eq-4", "20261018104000", new(2026, 10, 18, 10, 40, 0, TimeSpan.FromHours(9)), "茨城県南部");
		app.Network.Publish(next);
		await app.WaitAsync(
			() => Ui.VisibleTexts(app.EarthquakeWindow).Contains(next.Place),
			() => "PULL型で地震情報を受信する",
			TimeSpan.FromSeconds(10));
		app.Network.DmdataAvailable = false;
		await app.WaitPullAttemptsAsync(2);
		await AssertPullContinuesAsync();

		// 3回続けて失敗すると防災情報XMLに切り替える
		await app.WaitEarthquakeSourceAsync(JmaXml, advance: RecoveryLimit);

		async Task AssertPullContinuesAsync()
		{
			await app.SettleAsync();
			Assert.False(app.Earthquake.IsFault, $"PULL型の一時的な失敗で障害を表示しました。\n{app.Logs}");
			Assert.Equal(DmdataPull, app.Earthquake.SourceString);
			Assert.Equal(jmaRequests, app.Network.RequestCount(JmaFeedFetch));
		}
	}

	[AvaloniaFact]
	public async Task WebSocketが切断されると設定画面に今すぐ再接続が表示され_押すと再接続して表示が消える()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitDmdataWebSocketAsync();
		var settings = app.ShowDmdataSettings();
		var reconnect = Ui.FindButton(settings, "今すぐ再接続");
		await Ui.WaitUntilAsync(
			() => Ui.VisibleTexts(settings).Contains("接続状態: 完全接続") && !reconnect.IsEffectivelyVisible,
			() => $"接続中は再接続ボタンを表示しない\n{string.Join(", ", Ui.VisibleTexts(settings))}");

		app.Network.Socket!.Drop();
		await Ui.WaitUntilAsync(
			() => Ui.VisibleTexts(settings).Contains("接続状態: 未接続") && reconnect.IsEffectivelyVisible,
			() => $"切断中は再接続ボタンを表示する\n{string.Join(", ", Ui.VisibleTexts(settings))}");

		Ui.Click(settings, reconnect);
		await app.WaitEewReceivingAsync();
		await Ui.WaitUntilAsync(
			() => Ui.VisibleTexts(settings).Contains("接続状態: 完全接続") && !reconnect.IsEffectivelyVisible,
			() => $"再接続後は再接続ボタンを表示しない\n{string.Join(", ", Ui.VisibleTexts(settings))}");
		await app.AssertEewDeliveredAsync("20261018102500");
	}
}
