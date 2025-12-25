// Copyright (c) The Mapsui authors.
// SPDX-License-Identifier: MIT
// Original source: https://github.com/Mapsui/Mapsui

using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.CustomControl.Manipulations;

/// <summary>
/// フリング（慣性スクロール）を検出するトラッカー
/// </summary>
public class FlingTracker
{
	private const int MaxSize = 10;
	private const long MaxTicks = 80 * TimeSpan.TicksPerMillisecond;  // 過去80msのイベントのみ使用
	private readonly Queue<(double x, double y, long time)> _events = [];

	/// <summary>
	/// 最大速度（ピクセル/秒）
	/// </summary>
	public static double MaxVelocity { get; set; } = 4000.0;

	/// <summary>
	/// 最小速度（ピクセル/秒）- この値以下ではフリングしない
	/// </summary>
	public static double MinVelocity { get; set; } = 50.0;

	public void AddEvent(ScreenPosition position, long ticks)
	{
		_events.Enqueue((position.X, position.Y, ticks));

		if (_events.Count > 2)
		{
			while (_events.Count > MaxSize || _events.Peek().time < ticks - MaxTicks)
				_events.Dequeue();
		}
	}

	public void Restart() => _events.Clear();

	private (double vx, double vy, double v) CalcVelocity(long now)
	{
		double distanceX = 0;
		double distanceY = 0;

		var eventQueue = _events;
		var eventsArray = eventQueue.ToArray();

		if (eventsArray.Length == 0)
			return (0, 0, 0);

		(_, _, var firstTime) = eventsArray[0];

		long finalTime = 0;

		for (var i = 1; i < eventsArray.Length; i++)
		{
			(var lastX, var lastY, var lastTime) = eventsArray[i - 1];
			(var nowX, var nowY, var nowTime) = eventsArray[i];

			// 過去maxTicks以内のイベントのみ速度計算に使用
			if (now - lastTime < MaxTicks)
			{
				// ピクセル/秒で速度を計算
				distanceX += (nowX - lastX) * TimeSpan.TicksPerSecond;
				distanceY += (nowY - lastY) * TimeSpan.TicksPerSecond;
			}

			finalTime = nowTime;
		}

		var totalTime = finalTime - firstTime;

		var vx = distanceX / totalTime;
		var vy = distanceY / totalTime;
		var v = Math.Sqrt(vx * vx + vy * vy);
		return (vx, vy, v);
	}

	public void FlingIfNeeded(Action<double, double> onFling)
	{
		var (velocityX, velocityY, velocity) = CalcVelocity(DateTime.Now.Ticks);

		// 速度が閾値以下の場合はフリングしない
		if (velocity <= MinVelocity)
			return;

		// 異常な速度値を制限
		if (velocity > MaxVelocity && velocity > 0)
		{
			var scale = MaxVelocity / velocity;
			velocityX *= scale;
			velocityY *= scale;
		}

		onFling(velocityX, velocityY);
	}
}
