using KyoshinEewViewer.CustomControl.Manipulations;
using KyoshinEewViewer.Map;
using KyoshinMonitorLib;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.CustomControl;

/// <summary>
/// マニピュレーション後の慣性アニメーション（パン・ズーム両対応）
/// </summary>
internal class InertiaAnimation
{
	private readonly Stopwatch _stopwatch;
	private readonly double _initialPanVelocityX;
	private readonly double _initialPanVelocityY;
	private readonly double _initialZoomVelocity;
	private readonly ScreenPosition _zoomCenter;

	/// <summary>
	/// 減衰係数（大きいほど早く減速）
	/// </summary>
	public static double DecayRate { get; set; } = 5.0;

	/// <summary>
	/// 慣性アニメーションの最大持続時間
	/// </summary>
	public static TimeSpan MaxDuration { get; set; } = TimeSpan.FromMilliseconds(1000);

	/// <summary>
	/// 速度がこの値以下になったら停止
	/// </summary>
	private const double StopThreshold = 1.0;
	private const double ZoomStopThreshold = 0.001;

	public InertiaAnimation(ManipulationVelocity velocity)
	{
		_initialPanVelocityX = velocity.PanVelocityX;
		_initialPanVelocityY = velocity.PanVelocityY;
		_initialZoomVelocity = velocity.ZoomVelocity;
		_zoomCenter = velocity.LastCenter;
		_stopwatch = new Stopwatch();
	}

	/// <summary>
	/// アニメーション開始
	/// </summary>
	public void Start() => _stopwatch.Start();

	/// <summary>
	/// アニメーションが実行中かどうか
	/// </summary>
	public bool IsRunning
	{
		get
		{
			if (!_stopwatch.IsRunning)
				return false;

			if (_stopwatch.Elapsed > MaxDuration)
				return false;

			var t = _stopwatch.Elapsed.TotalSeconds;
			var decay = Math.Exp(-DecayRate * t);

			var currentPanVx = _initialPanVelocityX * decay;
			var currentPanVy = _initialPanVelocityY * decay;
			var currentZoomV = _initialZoomVelocity * decay;

			var panSpeed = Math.Sqrt(currentPanVx * currentPanVx + currentPanVy * currentPanVy);

			return panSpeed > StopThreshold || Math.Abs(currentZoomV) > ZoomStopThreshold;
		}
	}

	/// <summary>
	/// 現在のフレームでの変化量を計算
	/// </summary>
	public (double deltaX, double deltaY, double zoomDelta, ScreenPosition zoomCenter) GetDelta(double deltaTimeSeconds)
	{
		if (!_stopwatch.IsRunning)
			return (0, 0, 0, _zoomCenter);

		var t = _stopwatch.Elapsed.TotalSeconds;
		var decay = Math.Exp(-DecayRate * t);

		// 現在の速度
		var currentPanVx = _initialPanVelocityX * decay;
		var currentPanVy = _initialPanVelocityY * decay;
		var currentZoomV = _initialZoomVelocity * decay;

		// deltaTime秒間の移動量
		var deltaX = currentPanVx * deltaTimeSeconds;
		var deltaY = currentPanVy * deltaTimeSeconds;

		// ズームの変化量（対数スケールからリニアスケールへ）
		var zoomDelta = currentZoomV * deltaTimeSeconds;

		return (deltaX, deltaY, zoomDelta, _zoomCenter);
	}

	/// <summary>
	/// アニメーションを停止
	/// </summary>
	public void Stop() => _stopwatch.Stop();
}
