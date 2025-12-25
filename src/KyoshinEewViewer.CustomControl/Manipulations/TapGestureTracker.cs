// Copyright (c) The Mapsui authors.
// SPDX-License-Identifier: MIT
// Original source: https://github.com/Mapsui/Mapsui

using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.CustomControl.Manipulations;

/// <summary>
/// タップジェスチャー（シングルタップ、ダブルタップ、ロングプレス）を検出するトラッカー
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
	/// タップ検出処理
	/// </summary>
	/// <param name="tapEndPosition">タップ終了位置</param>
	/// <param name="maxTapDistance">タップと判定する最大移動距離</param>
	/// <param name="onTapped">タップ検出時のコールバック</param>
	/// <returns>イベントが処理されたかどうか</returns>
	public bool TapIfNeeded(ScreenPosition? tapEndPosition, double maxTapDistance, Func<ScreenPosition, GestureType, bool> onTapped)
	{
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
