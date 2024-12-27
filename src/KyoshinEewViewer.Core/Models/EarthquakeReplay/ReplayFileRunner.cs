using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core.Models.EarthquakeReplay;

public class ReplayFileRunner(IEnumerable<ReplayData> data)
{
	public event Action<DateTime, ReplayData[]>? DataArrived;
	public event Action<DateTime>? Finished;

	public bool IsPlaying { get; private set; }
	private Stopwatch Stopwatch { get; } = new Stopwatch();

	private DateTime CursorTime { get; set; }
	public DateTime CurrentTime => CursorTime + Stopwatch.Elapsed;

	public float Multiplier { get; set; } = 1;

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
			while (IsPlaying)
			{
				// 同じ時刻のデータをまとめて処理
				CursorTime = enumerator.Current.Time;
				var sameTimeData = new List<ReplayData>();
				var moveNext = false;
				do
				{
					sameTimeData.Add(enumerator.Current);
					moveNext = enumerator.MoveNext();
				} while (moveNext && enumerator.Current.Time <= CursorTime);

				Stopwatch.Restart();
				DataArrived?.Invoke(CursorTime, sameTimeData.ToArray());

				await Task.Delay((int)((enumerator.Current.Time - CursorTime).TotalMilliseconds / Multiplier));

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
