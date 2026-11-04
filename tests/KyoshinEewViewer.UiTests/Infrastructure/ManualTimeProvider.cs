namespace KyoshinEewViewer.UiTests.Infrastructure;

/// <summary>
/// テストから進める時刻<br/>
/// 再接続や復旧確認のタイマーを、実時間を待たずに発火させるために使う
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
	private readonly Lock _gate = new();
	private readonly List<ManualTimer> _timers = [];
	private DateTimeOffset _now = new(2026, 10, 18, 1, 0, 0, TimeSpan.Zero);

	public override DateTimeOffset GetUtcNow()
	{
		lock (_gate)
			return _now;
	}

	public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
	{
		var timer = new ManualTimer(this, callback, state);
		lock (_gate)
			_timers.Add(timer);
		timer.Change(dueTime, period);
		return timer;
	}

	/// <summary>
	/// 時刻を進め、その間に期限を迎えたタイマーを順番に発火させる
	/// </summary>
	public void Advance(TimeSpan duration)
	{
		DateTimeOffset target;
		lock (_gate)
			target = _now + duration;
		while (true)
		{
			ManualTimer? next;
			lock (_gate)
			{
				next = _timers.Where(t => t.Due <= target).MinBy(t => t.Due);
				if (next == null)
				{
					_now = target;
					return;
				}
				_now = next.Due!.Value;
				next.Due = next.Period > TimeSpan.Zero ? _now + next.Period : null;
			}
			next.Callback(next.State);
		}
	}

	private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
	{
		public TimerCallback Callback { get; } = callback;
		public object? State { get; } = state;
		public DateTimeOffset? Due { get; set; }
		public TimeSpan Period { get; private set; }

		public bool Change(TimeSpan dueTime, TimeSpan period)
		{
			lock (owner._gate)
			{
				Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
				Period = period == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : period;
			}
			return true;
		}

		public void Dispose()
		{
			lock (owner._gate)
			{
				Due = null;
				owner._timers.Remove(this);
			}
		}

		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}
	}
}
