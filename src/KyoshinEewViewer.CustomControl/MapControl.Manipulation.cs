using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl.Manipulations;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.CustomControl;

public partial class MapControl
{
	#region Manipulation
	private readonly Dictionary<IPointer, ScreenPosition> _positions = [];
	private readonly ManipulationTracker _manipulationTracker = new();
	private readonly FlingTracker _flingTracker = new();
	private readonly TapGestureTracker _tapGestureTracker = new();
	private InertiaAnimation? _inertiaAnimation;
	private DateTime _lastInertiaFrameTime;
	private bool _wasMultiTouch;

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		var pos = e.GetCurrentPoint(this).Position;
		_positions[e.Pointer] = new ScreenPosition(pos.X, pos.Y);

		// タッチ開始時はアニメーションを停止
		_inertiaAnimation?.Stop();
		_inertiaAnimation = null;
		NavigateAnimation = null;  // Navigate中でも操作を優先

		// 最初のタッチの場合
		if (_positions.Count == 1)
		{
			_wasMultiTouch = false;
			_flingTracker.Restart();
			_tapGestureTracker.Restart(new ScreenPosition(pos.X, pos.Y));
		}
		else
		{
			_wasMultiTouch = true;
		}

		// ManipulationTrackerをリスタート
		_manipulationTracker.Restart(GetPositions());

		base.OnPointerPressed(e);
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		if (!_positions.ContainsKey(e.Pointer))
			return;

		var pos = e.GetCurrentPoint(this).Position;
		_positions[e.Pointer] = new ScreenPosition(pos.X, pos.Y);

		// フリングトラッカーにイベントを追加（1点タッチの場合のみ）
		if (_positions.Count == 1)
			_flingTracker.AddEvent(new ScreenPosition(pos.X, pos.Y), DateTime.Now.Ticks);

		if (IsDisableManualControl || IsNavigating)
			return;

		// ManipulationTrackerでパン・ズームを処理
		_manipulationTracker.Manipulate(GetPositions(), OnManipulation);

		base.OnPointerMoved(e);
	}

	/// <summary>
	/// ManipulationTrackerからの操作を適用
	/// </summary>
	private void OnManipulation(Manipulation manipulation)
	{
		// スケール変更（ズーム）
		if (Math.Abs(manipulation.ScaleFactor - 1.0) > 0.001)
		{
			var newZoom = Math.Clamp(Zoom + Math.Log(manipulation.ScaleFactor, 2), MinZoom, MaxZoom);
			var zoomCenter = manipulation.Center;
			var zoomCenterLoc = GetLocation(new Point(zoomCenter.X, zoomCenter.Y));

			var newCenterPix = CenterLocation.ToPixel(newZoom);
			var goalCenterPix = zoomCenterLoc.ToPixel(newZoom);

			var paddedRect = PaddedRect;
			var newZoomCenterPix = new PointD(
				newCenterPix.X + ((paddedRect.Width / 2) - zoomCenter.X) + paddedRect.Left,
				newCenterPix.Y + ((paddedRect.Height / 2) - zoomCenter.Y) + paddedRect.Top);

			Zoom = newZoom;
			CenterLocation = (newCenterPix - (goalCenterPix - newZoomCenterPix)).ToLocation(newZoom);
		}

		// パン（移動）
		var deltaX = manipulation.Center.X - manipulation.PreviousCenter.X;
		var deltaY = manipulation.Center.Y - manipulation.PreviousCenter.Y;
		if (Math.Abs(deltaX) > 0.001 || Math.Abs(deltaY) > 0.001)
		{
			CenterLocation = (CenterLocation.ToPixel(Zoom) - new PointD(deltaX, deltaY)).ToLocation(Zoom);
		}
	}

	private ReadOnlySpan<ScreenPosition> GetPositions()
	{
		var positions = new ScreenPosition[_positions.Count];
		var i = 0;
		foreach (var pos in _positions.Values)
			positions[i++] = pos;
		return positions;
	}

	private Location GetLocation(Point p)
	{
		var centerPix = CenterLocation.ToPixel(Zoom);
		var originPix = new PointD(centerPix.X + ((PaddedRect.Width / 2) - p.X) + PaddedRect.Left, centerPix.Y + ((PaddedRect.Height / 2) - p.Y) + PaddedRect.Top);
		return originPix.ToLocation(Zoom);
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		var endPos = e.GetCurrentPoint(this).Position;

		// 指を離す前に慣性速度を取得（マルチタッチの場合）
		var inertiaVelocity = _wasMultiTouch ? _manipulationTracker.GetInertiaVelocity() : null;

		_positions.Remove(e.Pointer);

		// ManipulationTrackerを更新
		_manipulationTracker.Restart(GetPositions());

		// 全ての指が離れた場合のみジェスチャー処理を実行
		if (!IsDisableManualControl && !IsNavigating && _positions.Count == 0)
		{
			if (_wasMultiTouch)
			{
				// マルチタッチの場合は総合的な慣性アニメーションを開始
				if (inertiaVelocity is not null)
					StartInertiaAnimation(inertiaVelocity);
			}
			else
			{
				// シングルタッチの場合は従来通りタップ・フリング処理
				var handled = _tapGestureTracker.TapIfNeeded(
					new ScreenPosition(endPos.X, endPos.Y),
					8.0,
					(pos, gestureType) => OnGesture(pos, GetLocation(new Point(pos.X, pos.Y)), gestureType, e.InitialPressMouseButton));

				if (!handled)
				{
					_flingTracker.FlingIfNeeded(StartFlingAnimation);
				}
			}
		}

		base.OnPointerReleased(e);
	}

	/// <summary>
	/// ジェスチャーイベントを処理
	/// </summary>
	private bool OnGesture(ScreenPosition screenPosition, Location location, GestureType gestureType, MouseButton button)
	{
		switch (gestureType)
		{
			case GestureType.SingleTap:
				// レイヤーにクリックイベントを伝播
				LayerHost.OnMouseClick(location, new PointD(screenPosition.X, screenPosition.Y), button, RenderParameter);
				return true;

			case GestureType.DoubleTap:
				// ダブルタップでズームイン（タップ位置を中心に）
				if (!IsDisableManualControl)
				{
					var mousePos = new Point(screenPosition.X, screenPosition.Y);
					var newZoom = Math.Min(Zoom + 1, MaxZoom);
					if (Math.Abs(newZoom - Zoom) > 0.001)
					{
						var newCenterPix = CenterLocation.ToPixel(newZoom);
						var goalMousePix = location.ToPixel(newZoom);

						var paddedRect = PaddedRect;
						var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

						Zoom = newZoom;
						CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
					}
					return true;
				}
				return false;

			case GestureType.LongPress:
				// 将来の拡張用（コンテキストメニュー等）
				return false;

			default:
				return false;
		}
	}

	/// <summary>
	/// フリングアニメーションを開始（シングルタッチ用）
	/// </summary>
	private void StartFlingAnimation(double velocityX, double velocityY)
	{
		// FlingTrackerの速度をInertiaAnimationで使用
		// パン速度のみで、ズーム速度は0
		var velocity = new ManipulationVelocity(velocityX, velocityY, 0, new ScreenPosition(0, 0));
		StartInertiaAnimation(velocity);
	}

	/// <summary>
	/// 慣性アニメーションを開始
	/// </summary>
	private void StartInertiaAnimation(ManipulationVelocity velocity)
	{
		_inertiaAnimation = new InertiaAnimation(velocity);
		_inertiaAnimation.Start();
		_lastInertiaFrameTime = DateTime.Now;
		Dispatcher.UIThread.Post(InvalidateVisual);
	}

	/// <summary>
	/// 慣性アニメーションのフレームを処理
	/// </summary>
	private void ProcessInertiaFrame()
	{
		if (_inertiaAnimation is null || !_inertiaAnimation.IsRunning)
		{
			_inertiaAnimation = null;
			return;
		}

		var now = DateTime.Now;
		var deltaTime = (now - _lastInertiaFrameTime).TotalSeconds;
		_lastInertiaFrameTime = now;

		// 異常な値を防ぐ
		if (deltaTime <= 0 || deltaTime > 0.1)
			deltaTime = 0.016; // 約60fps

		var (deltaX, deltaY, zoomDelta, zoomCenter) = _inertiaAnimation.GetDelta(deltaTime);

		// ズームの適用
		if (Math.Abs(zoomDelta) > 0.0001)
		{
			var newZoom = Math.Clamp(Zoom + zoomDelta, MinZoom, MaxZoom);
			if (Math.Abs(newZoom - Zoom) > 0.0001)
			{
				var zoomCenterLoc = GetLocation(new Point(zoomCenter.X, zoomCenter.Y));
				var newCenterPix = CenterLocation.ToPixel(newZoom);
				var goalCenterPix = zoomCenterLoc.ToPixel(newZoom);

				var paddedRect = PaddedRect;
				var newZoomCenterPix = new PointD(
					newCenterPix.X + ((paddedRect.Width / 2) - zoomCenter.X) + paddedRect.Left,
					newCenterPix.Y + ((paddedRect.Height / 2) - zoomCenter.Y) + paddedRect.Top);

				Zoom = newZoom;
				CenterLocation = (newCenterPix - (goalCenterPix - newZoomCenterPix)).ToLocation(newZoom);
			}
		}

		// パンの適用
		if (Math.Abs(deltaX) > 0.001 || Math.Abs(deltaY) > 0.001)
		{
			CenterLocation = (CenterLocation.ToPixel(Zoom) - new PointD(deltaX, deltaY)).ToLocation(Zoom);
		}
	}

	protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
	{
		if (IsDisableManualControl || IsNavigating)
			return;

		var mousePos = e.GetCurrentPoint(this).Position;
		var mouseLoc = GetLocation(mousePos);

		var newZoom = Math.Clamp(Zoom + e.Delta.Y * 0.25, MinZoom, MaxZoom);
		if (Math.Abs(newZoom - Zoom) < .001)
			return;

		var newCenterPix = CenterLocation.ToPixel(newZoom);
		var goalMousePix = mouseLoc.ToPixel(newZoom);

		var paddedRect = PaddedRect;
		var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

		Zoom = newZoom;
		CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
		base.OnPointerWheelChanged(e);
	}
	#endregion Manipulation
}
