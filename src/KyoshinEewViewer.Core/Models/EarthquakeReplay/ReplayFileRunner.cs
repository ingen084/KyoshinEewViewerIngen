using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core.Models.EarthquakeReplay;

public class ReplayFileRunner(IEnumerable<ReplayData> data)
{
	public event Action<DateTime, ReplayData[]>? DataArrived;
	public event Action<DateTime>? Finished;

	public bool IsPlaying { get; private set; }
	private Stopwatch Stopwatch { get; } = new Stopwatch();

	private DateTime CursorTime { get; set; }
	public DateTime CurrentTime => CursorTime + (Stopwatch.Elapsed * SpeedMultiplier);

	public float SpeedMultiplier { get; set; } = 1;

	private Task RunnerTask { get; set; } = Task.CompletedTask;

	public void Start()
	{
		if (IsPlaying)
			return;
		Stopwatch.Start();
		IsPlaying = true;
		RunnerTask = Task.Run(async () =>
		{
			var enumerator = data.GetEnumerator();
			enumerator.MoveNext();
			CursorTime = enumerator.Current.Time;
			while (true)
			{
				// 同じ時刻のデータをまとめて処理
				var nextTime = enumerator.Current.Time;
				var sameTimeData = new List<ReplayData>();
				var moveNext = false;
				do
				{
					sameTimeData.Add(enumerator.Current);
					moveNext = enumerator.MoveNext();
				} while (moveNext && enumerator.Current.Time <= nextTime);

				// 次のイベントまで待機しつつ、時間が空いている場合は1秒ごとに空配列を送信
				while (true)
				{
					if (!IsPlaying)
						return;

					var waitMilliSeconds = (nextTime - CursorTime).TotalMilliseconds;
					if ((1000 - CursorTime.Millisecond) < waitMilliSeconds)
					{
						await Task.Delay((int)((1000 - CursorTime.Millisecond) / SpeedMultiplier));
						CursorTime = CursorTime.AddMilliseconds(1000 - CursorTime.Millisecond);
						DataArrived?.Invoke(CursorTime, Array.Empty<ReplayData>());
						Stopwatch.Restart();
						continue;
					}

					await Task.Delay((int)(waitMilliSeconds / SpeedMultiplier));
					break;
				}
				if (!IsPlaying)
					return;

				DataArrived?.Invoke(nextTime, sameTimeData.ToArray());
				CursorTime = nextTime;
				Stopwatch.Restart();

				if (!moveNext)
					break;
			}

			Finished?.Invoke(CursorTime);
			Stopwatch.Stop();
			IsPlaying = false;
		});
	}

	public async Task StopAsync()
	{
		if (!IsPlaying)
			return;
		IsPlaying = false;
		Stopwatch.Stop();
		await RunnerTask;
	}
}
