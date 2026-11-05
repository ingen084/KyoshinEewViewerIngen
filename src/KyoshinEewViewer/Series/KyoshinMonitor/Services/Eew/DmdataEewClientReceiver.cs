using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

/// <summary>
/// EEW Client との接続状態
/// </summary>
public enum DmdataEewClientConnectionState
{
	/// <summary>
	/// WebSocket に接続できていない (EEW Client 未起動・ポート誤りなど)
	/// </summary>
	Disconnected,
	/// <summary>
	/// WebSocket には接続できているが、EEW Client 側が dmdata.jp から受信できる状態ではない
	/// </summary>
	ConnectedNotReceiving,
	/// <summary>
	/// 緊急地震速報を受信できる状態
	/// </summary>
	Receiving,
}

/// <summary>
/// EEW Client by DMDATA.JP の外部連携機能へ WebSocket で接続し、緊急地震速報と予想地点の予測を受信する
/// </summary>
public partial class DmdataEewClientReceiver : ObservableObject, IDisposable
{
	/// <summary>
	/// 再接続の間隔 失敗するたびに倍にする
	/// </summary>
	private static readonly TimeSpan InitialReconnectInterval = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan MaxReconnectInterval = TimeSpan.FromSeconds(60);

	/// <summary>
	/// 接続の確立を待つ時間 ポートは開いているが応答しない場合に再接続へ進む
	/// </summary>
	private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

	/// <summary>
	/// 死活確認の間隔と応答を待つ時間 EEW Client が応答しなくなっても受信中と表示し続けないようにする
	/// </summary>
	private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(15);

	/// <summary>
	/// 受信バッファの上限 細分区域の予測を含むメッセージでも十分な大きさ
	/// </summary>
	private const int MaxMessageSize = 1024 * 1024;

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private EewController EewController { get; }
	private TimerService Timer { get; }

	/// <summary>
	/// 連携が有効になっているか (ポートが設定されていない場合は無効として扱う)
	/// </summary>
	[ObservableProperty]
	public partial bool IsEnabled { get; private set; }

	[ObservableProperty]
	public partial DmdataEewClientConnectionState State { get; private set; } = DmdataEewClientConnectionState.Disconnected;

	/// <summary>
	/// EEW Client から通知された dmdata.jp との接続状況 未受信の場合は null
	/// </summary>
	[ObservableProperty]
	public partial string? ServerStatus { get; private set; }

	/// <summary>
	/// 利用者向けの状態説明
	/// </summary>
	[ObservableProperty]
	public partial string StateDescription { get; private set; } = "";

	/// <summary>
	/// EEW Client から共有された予想地点の座標 接続ごとに通知されるため接続時に破棄する
	/// 受信ループからのみ読み書きする
	/// </summary>
	private Location? _userPointLocation;

	private readonly Lock _lock = new();
	private CancellationTokenSource? _tokenSource;
	private bool _isStarted;
	private bool _isDisposed;

	public DmdataEewClientReceiver(ILogger<DmdataEewClientReceiver> logger, KyoshinEewViewerConfiguration config, EewController eewController, TimerService timer)
	{
		Logger = logger;
		Config = config;
		EewController = eewController;
		Timer = timer;

		Config.Eew.DmdataEewClient.PropertyChanged += OnConfigChanged;
		UpdateStateDescription();
	}

	/// <summary>
	/// 接続を開始する 設定が無効な場合は何もしない
	/// </summary>
	public void Start()
	{
		lock (_lock)
		{
			// タイムシフトなどからリアルタイムへ戻るたびに呼ばれるため、受信を途切れさせないよう接続し直さない
			if (_isDisposed || _isStarted)
				return;
			_isStarted = true;
			RestartCore();
		}
	}

	private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
	{
		lock (_lock)
		{
			if (!_isStarted || _isDisposed)
				return;
			RestartCore();
		}
	}

	/// <summary>
	/// 現在の設定で接続をやり直す ロックを取得した状態で呼び出すこと
	/// </summary>
	private void RestartCore()
	{
		StopCore();

		var config = Config.Eew.DmdataEewClient;
		IsEnabled = config.Enable && config.Port is > 0 and <= ushort.MaxValue;
		// 接続先が変わった場合に、接続を確認できるまで前の状態を表示し続けないようにする
		State = DmdataEewClientConnectionState.Disconnected;
		ServerStatus = null;
		UpdateStateDescription();
		if (!IsEnabled)
			return;

		var tokenSource = new CancellationTokenSource();
		_tokenSource = tokenSource;
		var token = tokenSource.Token;
		var uri = new Uri($"ws://127.0.0.1:{config.Port}");
		_ = Task.Run(() => ConnectLoopAsync(uri, token), token);
	}

	private void StopCore()
	{
		if (_tokenSource == null)
			return;
		_tokenSource.Cancel();
		_tokenSource.Dispose();
		_tokenSource = null;
	}

	/// <summary>
	/// 切断されても再接続を続ける 停止後の状態は RestartCore 側で設定する
	/// </summary>
	private async Task ConnectLoopAsync(Uri uri, CancellationToken token)
	{
		var reconnectInterval = InitialReconnectInterval;
		// 未起動時にログが埋まらないよう、失敗が続く間は初回のみ記録する
		var isFailureLogged = false;

		while (!token.IsCancellationRequested)
		{
			using var webSocket = new ClientWebSocket();
			webSocket.Options.KeepAliveInterval = KeepAliveInterval;
			webSocket.Options.KeepAliveTimeout = KeepAliveTimeout;
			try
			{
				using (var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
				{
					connectTokenSource.CancelAfter(ConnectTimeout);
					await webSocket.ConnectAsync(uri, connectTokenSource.Token);
				}
				Logger.LogInformation("EEW Clientに接続しました: {Uri}", uri);
				reconnectInterval = InitialReconnectInterval;
				isFailureLogged = false;
				_userPointLocation = null;
				UpdateState(token, DmdataEewClientConnectionState.ConnectedNotReceiving, null);

				await ReceiveLoopAsync(webSocket, token);
				if (token.IsCancellationRequested)
					break;
				Logger.LogInformation("EEW Clientから切断されました");
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				if (token.IsCancellationRequested)
					break;
				if (!isFailureLogged)
				{
					Logger.LogWarning(ex, "EEW Clientとの通信に失敗しました 再接続を続けます: {Uri}", uri);
					isFailureLogged = true;
				}
				else
					Logger.LogDebug(ex, "EEW Clientとの通信に失敗しました: {Uri}", uri);
			}

			UpdateState(token, DmdataEewClientConnectionState.Disconnected, null);

			try
			{
				await Task.Delay(reconnectInterval, token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			reconnectInterval = TimeSpan.FromTicks(Math.Min(reconnectInterval.Ticks * 2, MaxReconnectInterval.Ticks));
		}
	}

	private async Task ReceiveLoopAsync(ClientWebSocket webSocket, CancellationToken token)
	{
		var buffer = new byte[16 * 1024];
		using var messageStream = new MemoryStream();

		while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
		{
			messageStream.SetLength(0);
			ValueWebSocketReceiveResult result;
			do
			{
				result = await webSocket.ReceiveAsync(buffer.AsMemory(), token);
				if (result.MessageType == WebSocketMessageType.Close)
				{
					await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, token);
					return;
				}
				if (messageStream.Length + result.Count > MaxMessageSize)
				{
					Logger.LogWarning("EEW Clientから受信したメッセージが大きすぎるため切断します");
					await webSocket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, null, token);
					return;
				}
				messageStream.Write(buffer, 0, result.Count);
			} while (!result.EndOfMessage);

			if (result.MessageType != WebSocketMessageType.Text)
				continue;

			try
			{
				HandleMessage(messageStream.GetBuffer().AsSpan(0, (int)messageStream.Length), token);
			}
			catch (Exception ex)
			{
				// 1件の解釈失敗で接続を落とさない
				Logger.LogWarning(ex, "EEW Clientからのメッセージの処理に失敗しました");
			}
		}
	}

	internal void HandleMessage(ReadOnlySpan<byte> json, CancellationToken token)
	{
		var message = JsonSerializer.Deserialize(json, DmdataEewClientSerializerContext.Default.DmdataEewClientMessage);
		if (message == null)
			return;

		switch (message.Type)
		{
			case "start":
				Logger.LogInformation("EEW Clientのバージョン: {Version}", message.Version);
				break;
			case "user-point":
				HandleUserPoint(message);
				break;
			case "server-status":
				SetServerStatus(message.Status, token);
				break;
			case "eew":
				HandleEew(message);
				break;
			default:
				Logger.LogDebug("EEW Clientから未知のメッセージを受信しました: {Type}", message.Type);
				break;
		}
	}

	private void HandleUserPoint(DmdataEewClientMessage message)
	{
		_userPointLocation = DmdataEewClientMessageConverter.ToUserPointLocation(message);
		if (_userPointLocation is { } location)
			Logger.LogInformation("EEW Clientから予想地点の座標を受信しました: {Latitude:0.000}, {Longitude:0.000}", location.Latitude, location.Longitude);
		else
			Logger.LogInformation("EEW Clientから予想地点の座標は共有されませんでした: {ValueKind}", message.Location?.ValueKind ?? JsonValueKind.Null);
	}

	private void HandleEew(DmdataEewClientMessage message)
	{
		// EEW Client で利用者が実行した訓練のデータも配信される
		if (message.IsTest)
		{
			Logger.LogInformation("EEW Clientから訓練データを受信したため処理しませんでした: {EventId} {SerialNo}報", message.EventId, message.Serial);
			return;
		}

		var receiveTime = Timer.CurrentTime;
		var eew = DmdataEewClientMessageConverter.ToEew(message, receiveTime);
		Logger.LogInformation("EEW Clientから緊急地震速報を受信しました: {EventId} {SerialNo}報", eew.Id, eew.SerialNo);

		// 取消報は Update では同一報番号として弾かれてしまうため、専用のキャンセル経路に流す
		if (eew.IsTrueCancelled)
		{
			EewController.Cancelled(eew.Id, receiveTime);
			return;
		}
		EewController.Update(eew, receiveTime);

		if (EewController.PointForecastController is not { } pointForecastController)
			return;
		// 精度が低いなどの理由で EEW 自体が取り込まれなかった場合は地点予測も取り込まない
		if (!EewController.TryGetEew(eew.Id, out _))
			return;

		if (DmdataEewClientMessageConverter.ToPointForecast(message, receiveTime, _userPointLocation) is { } forecast)
			pointForecastController.Update([forecast]);
		else
			// 予想が得られなくなった報で前の報の予想を表示し続けない
			pointForecastController.RemoveSource(eew.Id, DmdataEewClientMessageConverter.PointForecastSourceKey);
	}

	private void SetServerStatus(string? status, CancellationToken token)
	{
		Logger.LogInformation("EEW Clientの接続状況が変化しました: {Status}", status);
		UpdateState(
			token,
			status == "open" ? DmdataEewClientConnectionState.Receiving : DmdataEewClientConnectionState.ConnectedNotReceiving,
			status);
	}

	/// <summary>
	/// 接続ループから接続状態を更新する
	/// 再接続時に停止済みのループが新しいループの状態を上書きしないよう、キャンセルの確認と更新をロック内で行う
	/// </summary>
	private void UpdateState(CancellationToken token, DmdataEewClientConnectionState state, string? serverStatus)
	{
		lock (_lock)
		{
			if (token.IsCancellationRequested)
				return;
			ServerStatus = serverStatus;
			State = state;
			UpdateStateDescription();
		}
	}

	private void UpdateStateDescription()
	{
		if (!IsEnabled)
		{
			StateDescription = Config.Eew.DmdataEewClient.Enable
				? "EEW Clientの外部連携機能のポート番号を設定してください。"
				: "EEW Client連携は無効です。";
			return;
		}
		StateDescription = State switch
		{
			DmdataEewClientConnectionState.Receiving => "EEW Clientから緊急地震速報を受信しています。",
			DmdataEewClientConnectionState.ConnectedNotReceiving => "EEW Clientに接続していますが、緊急地震速報を受信できる状態ではありません。\n" + (ServerStatus switch
			{
				"no-auth" => "EEW ClientでDM-D.S.Sのアカウント連携が行われていません。",
				"no-contract" => "DM-D.S.Sの有効な契約がありません。",
				"limit" => "DM-D.S.SのWebSocket接続数が上限に達しています。",
				"close" => "EEW ClientがDM-D.S.Sから切断されています。",
				"connecting" => "EEW ClientがDM-D.S.Sへ接続中です。",
				null => "EEW Clientからの状態通知を待っています。",
				_ => $"EEW Clientの状態: {ServerStatus}",
			}),
			_ => "EEW Clientに接続できていません。EEW Clientの起動と外部連携機能の設定・ポート番号を確認してください。\n再接続は自動で行われます。",
		};
	}

	public void Dispose()
	{
		lock (_lock)
		{
			if (_isDisposed)
				return;
			_isDisposed = true;
			Config.Eew.DmdataEewClient.PropertyChanged -= OnConfigChanged;
			StopCore();
		}
		GC.SuppressFinalize(this);
	}
}
