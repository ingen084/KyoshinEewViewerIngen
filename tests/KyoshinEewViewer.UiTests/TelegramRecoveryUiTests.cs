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
	public async Task 防災情報XMLの受信中に回線が切れると障害を表示し_回線の復旧後は自動または再試行で受信を再開する()
	{
		await using var app = await TelegramScenario.StartAsync(useDmdata: false);
		await app.WaitEarthquakeSourceAsync(JmaXml);
		// dmdata を使わない場合、緊急地震速報は受信しない
		Assert.False(app.Eew.Enabled);

		// 定期取得で回線断を検知する
		app.Network.Online = false;
		await app.PollJmaAsync();
		await app.WaitFaultAsync();

		// 回線が戻ると自動で受信を再開する
		app.Network.Online = true;
		await app.WaitEarthquakeSourceAsync(JmaXml, advance: RecoveryLimit);
		await Ui.WaitUntilAsync(() => app.Tsunami.SourceName == JmaXml && !app.Tsunami.IsFault, () => $"津波情報を防災情報XMLから受信する\n{app.Logs}");

		// 再試行ボタンでは、自動の確認を待たずにすぐ受信を再開する
		app.Network.Online = false;
		await app.PollJmaAsync();
		await app.WaitFaultAsync();
		app.Network.Online = true;
		Ui.Click(app.EarthquakeWindow, Ui.FindButton(app.EarthquakeWindow, "再試行"));
		await app.WaitEarthquakeSourceAsync(JmaXml);
	}

	[AvaloniaFact]
	public async Task dmdataの受信中に回線が切れると緊急地震速報の切断と障害を表示し_回線の復旧後に緊急地震速報を含めて自動で受信を再開する()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket);
		await app.WaitEewReceivingAsync();

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
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket);
		await app.WaitEewReceivingAsync();

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
	public async Task WebSocketだけが切断されると緊急地震速報の切断を表示し_自動再接続後に切断中の地震情報を取得し直す()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitEarthquakeSourceAsync(DmdataWebSocket);
		await app.WaitEewReceivingAsync();

		var socket = app.Network.Socket!;
		socket.Drop();
		await Ui.WaitUntilAsync(() => app.Eew.IsDisconnected, () => $"緊急地震速報の切断を表示する\n{app.Logs}");
		// 地震情報は表示したまま待つ
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
	public async Task WebSocketが切断されると設定画面に今すぐ再接続が表示され_押すと再接続して表示が消える()
	{
		await using var app = await TelegramScenario.StartAsync();
		await app.WaitEewReceivingAsync();
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
