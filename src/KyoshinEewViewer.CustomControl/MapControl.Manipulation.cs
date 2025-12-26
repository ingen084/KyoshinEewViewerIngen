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
	private ManipulationVelocity? _lastMultiTouchVelocity;

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		var pos = e.GetCurrentPoint(this).Position;
		_positions[e.Pointer] = new ScreenPosition(pos.X, pos.Y);

		// タッチ開始時はアニメーションを停止
		_inertiaAnimation?.Stop();
		_inertiaAnimation = null;
		NavigateAnimation = null;  // Navigate中でも操作を優先
		_lastMultiTouchVelocity = null;

		// 最初のタッチの場合
		if (_positions.Count == 1)
		{
			_wasMultiTouch = false;
			_flingTracker.Restart();
			_tapGestureTracker.Restart(new ScreenPosition(pos.X, pos.Y), 8.0);
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
		var screenPos = new ScreenPosition(pos.X, pos.Y);
		_positions[e.Pointer] = screenPos;

		// フリングトラッカーにイベントを追加（1点タッチの場合のみ、ダブルタップドラッグ中は除く）
		if (_positions.Count == 1 && !_tapGestureTracker.IsDoubleTapDragging)
			_flingTracker.AddEvent(screenPos, DateTime.Now.Ticks);

		if (IsDisableManualControl || IsNavigating)
			return;

		// ダブルタップドラッグズームの処理
		if (_positions.Count == 1)
		{
			// ダブルタップドラッグモードの開始を試みる
			_tapGestureTracker.TryStartDoubleTapDrag(screenPos);

			// ダブルタップドラッグ中のズーム処理
			if (_tapGestureTracker.IsDoubleTapDragging)
			{
				var zoomDelta = _tapGestureTracker.GetDoubleTapDragZoomDelta(screenPos);
				if (Math.Abs(zoomDelta) > 0.001)
					Zoom += zoomDelta;
				return; // ダブルタップドラッグ中は通常のパン処理をスキップ
			}
		}

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

		// ダブルタップドラッグ中の場合はズーム慣性を取得
		var wasDoubleTapDragging = _tapGestureTracker.IsDoubleTapDragging;
		var zoomVelocity = wasDoubleTapDragging ? _tapGestureTracker.GetZoomVelocity() : 0;

		// 指を離す前に慣性速度を取得（マルチタッチで2本以上残っている場合のみ有効）
		// 1本になった時点でRestartが呼ばれるため、2本以上ある間に速度を保存しておく
		ManipulationVelocity? inertiaVelocity = null;
		if (_wasMultiTouch && _positions.Count >= 2)
		{
			inertiaVelocity = _manipulationTracker.GetInertiaVelocity();
			_lastMultiTouchVelocity = inertiaVelocity;
		}

		_positions.Remove(e.Pointer);

		// ManipulationTrackerを更新
		_manipulationTracker.Restart(GetPositions());

		// 全ての指が離れた場合のみジェスチャー処理を実行
		if (!IsDisableManualControl && !IsNavigating && _positions.Count == 0)
		{
			if (_wasMultiTouch)
			{
				// マルチタッチの場合は保存した慣性速度を使用
				if (_lastMultiTouchVelocity is not null)
					StartInertiaAnimation(_lastMultiTouchVelocity);
				_lastMultiTouchVelocity = null;
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
				// ダブルタップドラッグ終了時はズーム慣性を開始
				else if (wasDoubleTapDragging && Math.Abs(zoomVelocity) > 0.1)
				{
					StartZoomInertiaAnimation(zoomVelocity);
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
				// ダブルタップでズームイン（タップ位置を中心に、慣性アニメーション使用）
				if (!IsDisableManualControl && Zoom < MaxZoom)
				{
					StartDoubleTapZoomAnimation(screenPosition);
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
		if (!IsInertiaEnabled)
			return;

		_inertiaAnimation = new InertiaAnimation(velocity);
		_inertiaAnimation.Start();
		_lastInertiaFrameTime = DateTime.Now;
		Dispatcher.UIThread.Post(InvalidateVisual);
	}

	/// <summary>
	/// ズーム慣性アニメーションを開始（画面中心基準）
	/// </summary>
	private void StartZoomInertiaAnimation(double zoomVelocity)
	{
		if (!IsInertiaEnabled)
			return;

		// パン速度は0、ズーム速度のみ設定（中心はダミー）
		var velocity = new ManipulationVelocity(0, 0, zoomVelocity, new ScreenPosition(0, 0));
		_inertiaAnimation = new InertiaAnimation(velocity, useCenterZoom: true);
		_inertiaAnimation.Start();
		_lastInertiaFrameTime = DateTime.Now;
		Dispatcher.UIThread.Post(InvalidateVisual);
	}

	/// <summary>
	/// ダブルタップによるズームアニメーションを開始（タップ位置を中心に）
	/// </summary>
	private void StartDoubleTapZoomAnimation(ScreenPosition tapPosition)
	{
		var targetZoomDelta = 1.0;
		var newZoom = Math.Min(Zoom + targetZoomDelta, MaxZoom);
		var actualZoomDelta = newZoom - Zoom;

		if (Math.Abs(actualZoomDelta) < 0.001)
			return;

		// 慣性が無効の場合は即座にズーム
		if (!IsInertiaEnabled)
		{
			var mousePos = new Point(tapPosition.X, tapPosition.Y);
			var tapLocation = GetLocation(mousePos);

			var newCenterPix = CenterLocation.ToPixel(newZoom);
			var goalMousePix = tapLocation.ToPixel(newZoom);

			var paddedRect = PaddedRect;
			var newMousePix = new PointD(
				newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left,
				newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			Zoom = newZoom;
			CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
			return;
		}

		// 慣性アニメーションを使用してズーム
		// 減衰率を考慮して初期速度を計算（積分して目標ズーム量になるように）
		// ∫v₀*e^(-kt)dt = v₀/k * (1 - e^(-kt)) ≈ v₀/k (t→∞)
		// よって v₀ = targetZoomDelta * k
		var initialZoomVelocity = actualZoomDelta * InertiaAnimation.DecayRate;

		var velocity = new ManipulationVelocity(0, 0, initialZoomVelocity, tapPosition);
		_inertiaAnimation = new InertiaAnimation(velocity, useCenterZoom: false);
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
				if (_inertiaAnimation.UseCenterZoom)
				{
					// 画面中心基準のズーム（CenterLocationは変更しない）
					Zoom = newZoom;
				}
				else
				{
					// 特定点基準のズーム
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
