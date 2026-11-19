using System;
using System.Threading;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

/// <summary>
/// 強震モニタを受信できない状態が続いたことを検出し、受信できない間は取得の試行を指数バックオフで間引く。
/// </summary>
internal sealed class KyoshinMonitorConnectionMonitor(TimeProvider? timeProvider = null, Func<double>? nextRandom = null)
{
	/// <summary>取得の失敗がこの時間続いたら受信できていないとみなす</summary>
	public static readonly TimeSpan DisconnectThreshold = TimeSpan.FromSeconds(30);
	public static readonly TimeSpan InitialRetryInterval = TimeSpan.FromSeconds(5);
	public static readonly TimeSpan MaximumRetryInterval = TimeSpan.FromMinutes(1);
	/// <summary>こちらの時刻がサーバーよりこれ以上進んでいれば、時刻のずれが原因とみなす</summary>
	public static readonly TimeSpan ClockAheadThreshold = TimeSpan.FromSeconds(3);

	/// <summary>
	/// こちらの時刻がサーバーの時刻よりどれだけ進んでいるかを求める。遅れている場合は負の値になる
	/// </summary>
	/// <param name="requestedTime">要求した画像の時刻 (日本時間)</param>
	/// <param name="offsetMilliseconds">要求した時点でのオフセット</param>
	/// <param name="serverDate">応答の Date ヘッダー</param>
	public static TimeSpan? EstimateClockAhead(DateTime requestedTime, int offsetMilliseconds, DateTimeOffset? serverDate)
	{
		if (serverDate is not { } date)
			return null;
		// アプリ内の時刻は Kind によらず日本時間の値として扱われているため、Kind を変換せず値どうしで比べる
		var serverJst = date.UtcDateTime.AddHours(9);
		return requestedTime.AddMilliseconds(offsetMilliseconds) - serverJst;
	}

	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
	private readonly Func<double> _nextRandom = nextRandom ?? Random.Shared.NextDouble;
	private readonly Lock _lock = new();
	private long? _failureStartedTimestamp;
	private long _nextAttemptTimestamp;
	private TimeSpan _retryInterval;

	/// <summary>切断とみなしているか</summary>
	public bool IsDisconnected { get { lock (_lock) return _isDisconnected; } }
	private bool _isDisconnected;

	/// <summary>直近に予定した、次の再接続までの待ち時間</summary>
	public TimeSpan NextRetryDelay { get { lock (_lock) return _nextRetryDelay; } }
	private TimeSpan _nextRetryDelay;

	/// <summary>今回の取得を試みてよいか。切断中は再接続の予定時刻まで false を返す</summary>
	public bool ShouldAttempt()
	{
		lock (_lock)
			return !_isDisconnected || _timeProvider.GetTimestamp() >= _nextAttemptTimestamp;
	}

	/// <summary>サーバーから応答を受け取ったことを記録する</summary>
	/// <returns>切断から復帰した場合は、失敗が続いていた時間。それ以外は null</returns>
	public TimeSpan? ReportReachable()
	{
		lock (_lock)
		{
			TimeSpan? outage = _isDisconnected && _failureStartedTimestamp is { } started
				? _timeProvider.GetElapsedTime(started)
				: null;
			ResetCore();
			return outage;
		}
	}

	/// <summary>取得に失敗したことを記録する</summary>
	/// <param name="canDisconnect">
	/// false の場合、失敗が続いた時間には含めるが、これだけでは切断状態に入らない。
	/// 切断中であれば間隔を延ばさず、次の取得をすぐに許可する (オフセットの自動調整を止めないため)
	/// </param>
	/// <returns>この失敗で切断状態に入った場合 true</returns>
	public bool ReportFailure(bool canDisconnect = true)
	{
		lock (_lock)
		{
			var now = _timeProvider.GetTimestamp();
			_failureStartedTimestamp ??= now;
			if (!canDisconnect)
			{
				if (_isDisconnected)
					_nextAttemptTimestamp = now;
				return false;
			}
			if (_isDisconnected)
			{
				_retryInterval = TimeSpan.FromTicks(Math.Min(MaximumRetryInterval.Ticks, _retryInterval.Ticks * 2));
				ScheduleNextAttempt(now);
				return false;
			}
			if (_timeProvider.GetElapsedTime(_failureStartedTimestamp.Value, now) < DisconnectThreshold)
				return false;

			_isDisconnected = true;
			_retryInterval = InitialRetryInterval;
			ScheduleNextAttempt(now);
			return true;
		}
	}

	/// <summary>接続状態を初期状態に戻す</summary>
	/// <returns>切断中だった場合 true</returns>
	public bool Reset()
	{
		lock (_lock)
		{
			var wasDisconnected = _isDisconnected;
			ResetCore();
			return wasDisconnected;
		}
	}

	private void ResetCore()
	{
		_isDisconnected = false;
		_failureStartedTimestamp = null;
		_nextAttemptTimestamp = 0;
		_retryInterval = TimeSpan.Zero;
		_nextRetryDelay = TimeSpan.Zero;
	}

	private void ScheduleNextAttempt(long now)
	{
		// 多数のクライアントが同じ間隔で再接続を繰り返さないよう ±25% 揺らす。揺らした後も上限は超えない
		var jittered = _retryInterval * (0.75 + _nextRandom() * 0.5);
		_nextRetryDelay = jittered < MaximumRetryInterval ? jittered : MaximumRetryInterval;
		_nextAttemptTimestamp = now + (long)(_nextRetryDelay.TotalSeconds * _timeProvider.TimestampFrequency);
	}
}
