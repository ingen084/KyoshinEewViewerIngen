// Copyright (c) The Mapsui authors.
// SPDX-License-Identifier: MIT
// Original source: https://github.com/Mapsui/Mapsui

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KyoshinEewViewer.CustomControl.Manipulations;

/// <summary>
/// タップジェスチャー（シングルタップ、ダブルタップ、ロングプレス、ダブルタップドラッグ）を検出するトラッカー
/// </summary>
public class TapGestureTracker
{
	private readonly double _maxTapDuration = 0.5;
	private readonly double _noTapInterval = 0.25;
	private readonly double _maxLongTapDuration = 3;
	private DateTime _tapStartTime;
	private ScreenPosition? _tapStartPosition;
	private readonly int _millisecondsToWaitForDoubleTap = 300;
	private bool _waitingForDoubleTap;
	private ScreenPosition? _previousTapPosition;

	/// <summary>
	/// ダブルタップドラッグモードかどうか
	/// </summary>
	public bool IsDoubleTapDragging { get; private set; }

	/// <summary>
	/// 前回のY座標（ズーム変化量計算用）
	/// </summary>
	private double _previousY;

	/// <summary>
	/// ダブルタップ後のドラッグ検出用の閾値（ピクセル）
	/// </summary>
	private const double DoubleTapDragThreshold = 10.0;

	/// <summary>
	/// ズーム速度計算用のイベントキュー
	/// </summary>
	private const int MaxZoomSamples = 10;
	private const long MaxZoomTicks = 80 * TimeSpan.TicksPerMillisecond;  // 過去80msのイベントのみ使用
	private readonly Queue<(double zoomDelta, long time)> _zoomEvents = [];

	/// <summary>
	/// 最大ズーム速度（ズームレベル/秒）
	/// </summary>
	public static double MaxZoomVelocity { get; set; } = 5.0;

	/// <summary>
	/// 最小ズーム速度（ズームレベル/秒）- この値以下では慣性を適用しない
	/// </summary>
	public static double MinZoomVelocity { get; set; } = 0.1;

	/// <summary>
	/// タップ検出処理
	/// </summary>
	/// <param name="tapEndPosition">タップ終了位置</param>
	/// <param name="maxTapDistance">タップと判定する最大移動距離</param>
	/// <param name="onTapped">タップ検出時のコールバック</param>
	/// <returns>イベントが処理されたかどうか</returns>
	public bool TapIfNeeded(ScreenPosition? tapEndPosition, double maxTapDistance, Func<ScreenPosition, GestureType, bool> onTapped)
	{
		// ダブルタップドラッグモードを終了（慣性用の速度は保持）
		if (IsDoubleTapDragging)
		{
			IsDoubleTapDragging = false;
			return true; // ドラッグ終了として処理済み
		}

		if (_tapStartPosition is null)
			return false;
		if (tapEndPosition is null)
			return false;

		var duration = (DateTime.Now - _tapStartTime).TotalSeconds;
		var distance = tapEndPosition.Value.Distance(_tapStartPosition.Value);
		var isTap = duration < _maxTapDuration && distance < maxTapDistance;

		if (isTap)
		{
			if (_waitingForDoubleTap)
			{
				if (_previousTapPosition is null)
					return false;
				var distanceToPreviousTap = tapEndPosition.Value.Distance(_previousTapPosition.Value);
				_previousTapPosition = null;
				if (duration < _maxTapDuration && distanceToPreviousTap < maxTapDistance)
					return onTapped(tapEndPosition.Value, GestureType.DoubleTap);
			}
			else
			{
				_previousTapPosition = tapEndPosition;
				_ = StartWaitingForSecondTapAsync();
				return onTapped(tapEndPosition.Value, GestureType.SingleTap);
			}
		}
		else
		{
			var minLongTapDuration = _maxTapDuration + _noTapInterval;
			var isLongTap =
				duration > minLongTapDuration
				&& duration < _maxLongTapDuration
				&& distance < maxTapDistance;

			if (isLongTap)
				return onTapped(tapEndPosition.Value, GestureType.LongPress);
		}
		return false;
	}

	/// <summary>
	/// ダブルタップドラッグズームの開始を試みる
	/// </summary>
	/// <param name="currentPosition">現在の位置</param>
	/// <param name="maxTapDistance">タップと判定する最大移動距離</param>
	/// <returns>ダブルタップドラッグモードが開始されたかどうか</returns>
	public bool TryStartDoubleTapDrag(ScreenPosition currentPosition, double maxTapDistance)
	{
		if (IsDoubleTapDragging)
			return true;

		if (!_waitingForDoubleTap || _previousTapPosition is null || _tapStartPosition is null)
			return false;

		var duration = (DateTime.Now - _tapStartTime).TotalSeconds;
		if (duration > _maxTapDuration)
			return false;

		// 前回のタップ位置と現在のタッチ開始位置が近いか
		var distanceToPreviousTap = _tapStartPosition.Value.Distance(_previousTapPosition.Value);
		if (distanceToPreviousTap > maxTapDistance)
			return false;

		// 現在位置がタッチ開始位置から閾値以上離れているか（ドラッグ開始）
		var dragDistance = currentPosition.Distance(_tapStartPosition.Value);
		if (dragDistance >= DoubleTapDragThreshold)
		{
			IsDoubleTapDragging = true;
			_previousY = currentPosition.Y;
			_zoomEvents.Clear();
			_waitingForDoubleTap = false;
			_previousTapPosition = null;
			return true;
		}

		return false;
	}

	/// <summary>
	/// ダブルタップドラッグによるズーム変化量を計算し、速度計算用のイベントをキューに追加
	/// </summary>
	/// <param name="currentPosition">現在の位置</param>
	/// <param name="sensitivity">感度（ピクセルあたりのズーム変化量）</param>
	/// <returns>ズーム変化量（正:ズームイン、負:ズームアウト）</returns>
	public double GetDoubleTapDragZoomDelta(ScreenPosition currentPosition, double sensitivity = 0.01)
	{
		if (!IsDoubleTapDragging)
			return 0;

		var now = DateTime.Now.Ticks;

		// 下にドラッグ → ズームイン（正）、上にドラッグ → ズームアウト（負）
		var deltaY = currentPosition.Y - _previousY;
		var zoomDelta = deltaY * sensitivity;

		// イベントをキューに追加
		_zoomEvents.Enqueue((zoomDelta, now));

		// 古いイベントを削除
		if (_zoomEvents.Count > 2)
		{
			while (_zoomEvents.Count > MaxZoomSamples || _zoomEvents.Peek().time < now - MaxZoomTicks)
				_zoomEvents.Dequeue();
		}

		_previousY = currentPosition.Y;

		return zoomDelta;
	}

	/// <summary>
	/// 現在のズーム速度を取得（慣性アニメーション用）
	/// 過去のイベントから速度を計算
	/// </summary>
	/// <returns>ズーム速度（ズームレベル/秒）</returns>
	public double GetZoomVelocity()
	{
		var now = DateTime.Now.Ticks;
		var eventsArray = _zoomEvents.ToArray();

		if (eventsArray.Length < 2)
			return 0;

		double totalZoomDelta = 0;
		long firstValidTime = 0;
		long lastTime = 0;
		var hasValidEvent = false;

		for (var i = 0; i < eventsArray.Length; i++)
		{
			var (zoomDelta, time) = eventsArray[i];

			// 過去MaxZoomTicks以内のイベントのみ使用
			if (now - time < MaxZoomTicks)
			{
				if (!hasValidEvent)
				{
					firstValidTime = time;
					hasValidEvent = true;
				}
				totalZoomDelta += zoomDelta;
				lastTime = time;
			}
		}

		if (!hasValidEvent || lastTime == firstValidTime)
			return 0;

		var totalTime = (lastTime - firstValidTime) / (double)TimeSpan.TicksPerSecond;
		if (totalTime <= 0)
			return 0;

		var velocity = totalZoomDelta / totalTime;

		// 速度を制限
		velocity = Math.Clamp(velocity, -MaxZoomVelocity, MaxZoomVelocity);

		// 最小速度未満は0とする
		if (Math.Abs(velocity) < MinZoomVelocity)
			return 0;

		return velocity;
	}

	private async Task StartWaitingForSecondTapAsync()
	{
		_waitingForDoubleTap = true;
		await Task.Delay(_millisecondsToWaitForDoubleTap);
		_waitingForDoubleTap = false;
	}

	public void Restart(ScreenPosition position)
	{
		_tapStartTime = DateTime.Now;
		_tapStartPosition = position;
	}
}
