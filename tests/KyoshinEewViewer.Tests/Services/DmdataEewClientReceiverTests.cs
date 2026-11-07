using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Tests.Services;

public class DmdataEewClientReceiverTests
{
	private const string EventId = "20260914013000";

	private sealed record Fixture(
		KyoshinEewViewerConfiguration Config,
		EewController Controller,
		DmdataEewClientReceiver Receiver,
		List<Eew[]> Updates) : IDisposable
	{
		public void Receive(string json) => Receiver.HandleMessage(Encoding.UTF8.GetBytes(json), CancellationToken.None);

		public void Dispose() => Receiver.Dispose();
	}

	private static Fixture CreateFixture()
	{
		var config = new KyoshinEewViewerConfiguration();
		var timer = new TimerService(NullLogger<TimerService>.Instance, config);
		var controller = new EewController(
			NullLogger<EewController>.Instance,
			null!,
			config,
			new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance),
			new WorkflowService(NullLogger<WorkflowService>.Instance));
		_ = new EewPointForecastController(NullLogger<EewPointForecastController>.Instance, config, controller, timer);
		var updates = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updates.Add(eews);
		return new(config, controller, new(NullLogger<DmdataEewClientReceiver>.Instance, config, controller, timer), updates);
	}

	private static string EewMessage(int serial, string maxInt = "3", string regionInt = "3", bool isTest = false, bool isCanceled = false, string pointForecast = "")
		=> $$"""
			{"type":"eew","eventId":"{{EventId}}","serial":"{{serial}}","author":"気象庁本庁",
			 "isTest":{{(isTest ? "true" : "false")}},"isCanceled":{{(isCanceled ? "true" : "false")}},"isLastInfo":false,"isPlumOnly":false,"isLevelOver":false,"isWarning":false,
			 "epicenterName":"石川県能登地方","epicenterLocation":[137.2,37.5],"depth":10,"magnitude":5.7,
			 "originTime":"2026-09-14T01:30:00+09:00","arrivalTime":"2026-09-14T01:30:02+09:00",
			 "accuracy":{"epicenters":["3","3"],"depth":"3","magnitudeCalculation":"2"},
			 "maxInt":{"from":"{{maxInt}}","to":"{{maxInt}}"},
			 "regions":[{"code":"390","maxInt":"{{regionInt}}"}]{{pointForecast}}}
			""";

	private static string PointForecast(string? k)
		=> $$$""","pointForecast":{"sWave":{"status":"not-calculated","time":null},"intensity":{"type":"attenuation","k":{{{(k == null ? "null" : $"\"{k}\"")}}},"int":null}}""";

	[Fact(DisplayName = "訓練を受信しない設定では、訓練データを EEW として処理せず、同じイベントの実際の EEW を更新・取消もしない")]
	public void 訓練を受信しない設定では訓練データをEEWとして処理しない()
	{
		using var fixture = CreateFixture();

		fixture.Receive(EewMessage(1, isTest: true));
		Assert.Empty(fixture.Updates);
		Assert.False(fixture.Controller.TryGetEew(EventId, out _));

		fixture.Receive(EewMessage(1));
		var updateCount = fixture.Updates.Count;
		Assert.True(updateCount > 0);

		fixture.Receive(EewMessage(2, maxInt: "7", isTest: true));
		fixture.Receive(EewMessage(3, isTest: true, isCanceled: true));

		Assert.Equal(updateCount, fixture.Updates.Count);
		Assert.True(fixture.Controller.TryGetEew(EventId, out var eew));
		Assert.Equal(1, eew?.SerialNo);
		Assert.Equal(JmaIntensity.Int3, eew?.MaxIntensity);
		Assert.False(eew?.IsCancelled);
	}

	[Fact(DisplayName = "共有された予想地点を公開し、地点予測には設定した名前を使い、名前を変えると前の名前の地点予測を残さない")]
	public void 地点予測には設定した予想地点の名前を使う()
	{
		using var fixture = CreateFixture();
		Assert.Null(fixture.Receiver.UserPointLocation);

		fixture.Receive("""{"type":"user-point","location":[139.7,35.6]}""");
		var location = fixture.Receiver.UserPointLocation;
		Assert.NotNull(location);
		Assert.Equal((35.6f, 139.7f), (location.Latitude, location.Longitude));

		fixture.Receive(EewMessage(1, pointForecast: PointForecast("2.6")));
		var forecast = Assert.Single(Assert.Single(fixture.Updates.Last()).PointForecasts!);
		Assert.Equal(DmdataEewClientMessageConverter.DefaultPointName, forecast.PointName);
		Assert.Same(location, forecast.Location);

		fixture.Config.Eew.DmdataEewClient.PointName = " 自宅 ";
		fixture.Receive(EewMessage(2, pointForecast: PointForecast("3.6")));
		forecast = Assert.Single(Assert.Single(fixture.Updates.Last()).PointForecasts!);
		Assert.Equal("自宅", forecast.PointName);
		Assert.Equal(3.6, forecast.RealtimeIntensity);
	}

	[Fact(DisplayName = "訓練を受信する設定では、訓練を同じ地震IDの実際の EEW と区別し、実際の EEW を受信すると訓練を終了して報数・地点予測・取消を引き継がない")]
	public void 訓練を受信する設定では訓練を実際のEEWと区別する()
	{
		using var fixture = CreateFixture();
		fixture.Config.Eew.DmdataEewClient.ReceiveTraining = true;

		fixture.Receive(EewMessage(2, maxInt: "7", isTest: true, pointForecast: PointForecast("5.6")));
		var training = Assert.Single(fixture.Updates.Last());
		Assert.True(training.IsTraining);
		Assert.Equal(EventId, training.Id);
		Assert.Equal((2, JmaIntensity.Int7), (training.SerialNo, training.MaxIntensity));
		Assert.Equal(5.6, Assert.Single(training.PointForecasts!).RealtimeIntensity);
		Assert.False(fixture.Controller.TryGetEew(EventId, out _));

		// 訓練より小さい報数でも実際の EEW として受信する
		fixture.Receive(EewMessage(1, pointForecast: PointForecast("2.6")));
		var real = Assert.Single(fixture.Updates.Last());
		Assert.False(real.IsTraining);
		Assert.Equal((1, JmaIntensity.Int3), (real.SerialNo, real.MaxIntensity));
		Assert.Equal(2.6, Assert.Single(real.PointForecasts!).RealtimeIntensity);

		fixture.Receive(EewMessage(3, isTest: true, isCanceled: true));
		Assert.False(Assert.Single(fixture.Updates.Last()).IsCancelled);
	}

	[Fact(DisplayName = "同じ報数の再配信で予想震度と地点予測が更新され、予想が無くなると地点予測を残さない")]
	public void 同じ報数の再配信で予想震度と地点予測が更新される()
	{
		using var fixture = CreateFixture();

		fixture.Receive(EewMessage(1, pointForecast: PointForecast("2.6")));
		fixture.Receive(EewMessage(1, maxInt: "5-", regionInt: "5-", pointForecast: PointForecast("4.6")));

		var redelivered = Assert.Single(fixture.Updates.Last());
		Assert.Equal(JmaIntensity.Int5Lower, redelivered.MaxIntensity);
		Assert.Equal(JmaIntensity.Int5Lower, redelivered.CalculatedIntensityForecastMap?[390]);
		Assert.Equal(4.6, Assert.Single(redelivered.PointForecasts!).RealtimeIntensity);

		// 予測震度が null の場合と、pointForecast 自体が無い場合
		foreach (var (serial, pointForecast) in new[] { (2, PointForecast(null)), (4, "") })
		{
			fixture.Receive(EewMessage(serial, pointForecast: pointForecast));
			Assert.Null(Assert.Single(fixture.Updates.Last()).PointForecasts);

			fixture.Receive(EewMessage(serial + 1, pointForecast: PointForecast("3.6")));
			Assert.Single(Assert.Single(fixture.Updates.Last()).PointForecasts!);
		}
	}

	[Fact(DisplayName = "取消報は他の受信元から受信した EEW に適用した場合も外部への送信を禁止する")]
	public void 取消報は他の受信元のEEWに適用した場合も外部への送信を禁止する()
	{
		using var fixture = CreateFixture();
		var time = new DateTime(2026, 9, 14, 1, 30, 5);
		fixture.Controller.Update(EewMock.NORMAL with
		{
			Id = EventId,
			Source = EewSource.KyoshinMonitor,
			SerialNo = 1,
			IsCancelled = false,
			IsTrueCancelled = false,
			ReceiveTime = time,
		}, time);

		fixture.Receive(EewMessage(2, isCanceled: true));

		Assert.True(fixture.Controller.TryGetEew(EventId, out var eew));
		Assert.Equal(EewSource.KyoshinMonitor, eew?.Source);
		Assert.True(eew?.IsTrueCancelled);
		Assert.True(eew?.IsCancellationRelayProhibited);
	}

	[Fact(DisplayName = "接続状態は EEW Client の通知に追従し、接続先の設定の変更では前の状態を残さず接続し直し、再度の開始と接続先に関わらない設定の変更では接続を維持する")]
	public async Task 接続状態はEEWClientの通知と設定の変更に追従する()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var token = timeout.Token;
		using var fixture = CreateFixture();
		var receiver = fixture.Receiver;
		var config = fixture.Config.Eew.DmdataEewClient;
		using var first = new LoopbackEewClient();
		using var second = new LoopbackEewClient();

		config.Enable = true;
		config.Port = first.Port;
		receiver.Start();
		var firstSocket = await first.AcceptAsync(token);
		await WaitUntilAsync(() => receiver.State == DmdataEewClientConnectionState.ConnectedNotReceiving, "接続", token);

		await SendAsync(firstSocket, """{"type":"server-status","status":"no-contract"}""", token);
		await WaitUntilAsync(() => receiver.StateDescription.Contains("契約"), "受信できない理由の表示", token);
		Assert.Equal(DmdataEewClientConnectionState.ConnectedNotReceiving, receiver.State);

		await SendAsync(firstSocket, """{"type":"server-status","status":"open"}""", token);
		await WaitUntilAsync(() => receiver.State == DmdataEewClientConnectionState.Receiving, "受信可能", token);

		// 接続先に関わらない設定は、切り替えても同じ接続で受信を続ける
		config.ReceiveTraining = true;
		config.PointName = "自宅";
		config.ShowPointOnMap = true;
		Assert.Equal(DmdataEewClientConnectionState.Receiving, receiver.State);
		await SendAsync(firstSocket, EewMessage(1, isTest: true), token);
		await WaitUntilAsync(() => fixture.Controller.TryGetEew(Eew.GetKey(EventId, true), out _), "訓練の受信", token);

		// リアルタイムへ戻るたびに呼ばれても同じ接続で受信を続ける
		receiver.Start();
		await SendAsync(firstSocket, EewMessage(1), token);
		await WaitUntilAsync(() => fixture.Controller.TryGetEew(EventId, out _), "EEWの受信", token);
		Assert.Equal(DmdataEewClientConnectionState.Receiving, receiver.State);

		// 新しい接続先へ接続できるまで受信中と表示しない
		config.Port = second.Port;
		Assert.Equal(DmdataEewClientConnectionState.Disconnected, receiver.State);
		var secondSocket = await second.AcceptAsync(token);
		await WaitUntilAsync(() => receiver.State == DmdataEewClientConnectionState.ConnectedNotReceiving, "別のポートへの接続", token);

		await secondSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, token);
		await WaitUntilAsync(() => receiver.State == DmdataEewClientConnectionState.Disconnected, "切断の検知", token);

		config.Port = 0;
		Assert.False(receiver.IsEnabled);
		Assert.Contains("ポート番号", receiver.StateDescription);

		config.Enable = false;
		Assert.Contains("無効", receiver.StateDescription);
	}

	private static async Task WaitUntilAsync(Func<bool> condition, string description, CancellationToken token)
	{
		while (!condition())
		{
			if (token.IsCancellationRequested)
				throw new TimeoutException($"{description}を待っている間に時間切れになりました");
			await Task.Delay(10, CancellationToken.None);
		}
	}

	private static Task SendAsync(WebSocket socket, string json, CancellationToken token)
		=> socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, token);

	/// <summary>
	/// EEW Client の外部連携機能の代わりにループバックで待ち受ける WebSocket サーバー
	/// </summary>
	private sealed class LoopbackEewClient : IDisposable
	{
		private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
		private readonly List<TcpClient> _clients = [];

		public LoopbackEewClient() => _listener.Start();

		public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

		public async Task<WebSocket> AcceptAsync(CancellationToken token)
		{
			var client = await _listener.AcceptTcpClientAsync(token);
			_clients.Add(client);
			var stream = client.GetStream();

			var buffer = new byte[4096];
			var length = 0;
			while (!Encoding.ASCII.GetString(buffer, 0, length).EndsWith("\r\n\r\n", StringComparison.Ordinal))
			{
				var read = await stream.ReadAsync(buffer.AsMemory(length), token);
				if (read == 0)
					throw new IOException("ハンドシェイクの途中で切断されました");
				length += read;
			}

			var key = Regex.Match(Encoding.ASCII.GetString(buffer, 0, length), @"Sec-WebSocket-Key:\s*(\S+)", RegexOptions.IgnoreCase).Groups[1].Value;
			var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
			await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"), token);
			return WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: Timeout.InfiniteTimeSpan);
		}

		public void Dispose()
		{
			foreach (var client in _clients)
				client.Dispose();
			_listener.Stop();
		}
	}
}
