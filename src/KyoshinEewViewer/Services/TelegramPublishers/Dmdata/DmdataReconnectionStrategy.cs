using System;
using System.Threading;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

/// <summary>
/// 再接続とリトライの戦略を管理する
/// </summary>
public class DmdataReconnectionStrategy : IDisposable
{
	private Timer? WebSocketReconnectTimer { get; set; }
	private Timer? TemporaryFailureRecoveryTimer { get; set; }

	/// <summary>
	/// WebSocket再接続のバックオフ時間(秒)
	/// </summary>
	public int ReconnectBackoffTime { get; private set; } = 10;

	/// <summary>
	/// 一時的な障害の失敗回数
	/// </summary>
	public int TemporaryFailureCount { get; private set; }

	/// <summary>
	/// 最後に一時的な障害が発生した時刻
	/// </summary>
	public DateTime? LastTemporaryFailureTime { get; private set; }

	/// <summary>
	/// WebSocket再接続タイマーを初期化する
	/// </summary>
	/// <param name="reconnectCallback">再接続時に呼び出されるコールバック</param>
	public void InitializeWebSocketReconnectTimer(Func<bool> shouldReconnect, Action reconnectAction)
	{
		WebSocketReconnectTimer = new Timer(s =>
		{
			if (shouldReconnect())
			{
				reconnectAction();
				ReconnectBackoffTime = Math.Min(600, ReconnectBackoffTime * 2);
			}
			WebSocketReconnectTimer?.Change(TimeSpan.FromSeconds(ReconnectBackoffTime), Timeout.InfiniteTimeSpan);
		}, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// 一時障害復旧タイマーを初期化する
	/// </summary>
	/// <param name="recoveryAction">復旧時に呼び出されるアクション</param>
	public void InitializeTemporaryFailureRecoveryTimer(Action recoveryAction)
	{
		TemporaryFailureRecoveryTimer = new Timer(s =>
		{
			recoveryAction();
		}, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// WebSocket再接続タイマーを開始する
	/// </summary>
	public void StartWebSocketReconnectTimer()
	{
		WebSocketReconnectTimer?.Change(TimeSpan.FromSeconds(ReconnectBackoffTime), Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// WebSocket再接続タイマーを停止する
	/// </summary>
	public void StopWebSocketReconnectTimer()
	{
		WebSocketReconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite);
	}

	/// <summary>
	/// 一時障害復旧タイマーを停止する
	/// </summary>
	public void StopTemporaryFailureRecoveryTimer()
	{
		TemporaryFailureRecoveryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
	}

	/// <summary>
	/// バックオフ時間をリセットする
	/// </summary>
	public void ResetBackoffTime()
	{
		ReconnectBackoffTime = 10;
	}

	/// <summary>
	/// 一時的な障害を記録し、復旧タイマーをスケジュールする
	/// </summary>
	public void RecordTemporaryFailure()
	{
		TemporaryFailureCount++;
		LastTemporaryFailureTime = DateTime.Now;

		// 指数バックオフで再試行間隔を計算 (10秒から最大300秒)
		var retryInterval = Math.Min(300, 10 * Math.Pow(2, Math.Min(TemporaryFailureCount - 1, 5)));
		TemporaryFailureRecoveryTimer?.Change(TimeSpan.FromSeconds(retryInterval), Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// 一時的な障害状態をリセットする
	/// </summary>
	public void ResetTemporaryFailure()
	{
		TemporaryFailureCount = 0;
		LastTemporaryFailureTime = null;
		StopTemporaryFailureRecoveryTimer();
	}

	/// <summary>
	/// 即座に再接続を実行する(バックオフをリセット)
	/// </summary>
	public void ReconnectImmediately()
	{
		StopWebSocketReconnectTimer();
		ReconnectBackoffTime = 5;
	}

	public void Dispose()
	{
		WebSocketReconnectTimer?.Dispose();
		TemporaryFailureRecoveryTimer?.Dispose();
		GC.SuppressFinalize(this);
	}
}
