using System;

namespace KyoshinEewViewer.CustomControl.Manipulations;

/// <summary>
/// マウスホイールによるズーム操作の慣性を管理するトラッカー
/// </summary>
public class WheelZoomTracker
{
	/// <summary>
	/// ホイールズーム慣性の減衰係数
	/// </summary>
	public static double DecayRate { get; set; } = 8.0;

	/// <summary>
	/// ホイール1回あたりの速度加算量
	/// </summary>
	public static double VelocityPerStep { get; set; } = 2.0;

	/// <summary>
	/// ホイールズーム慣性の停止閾値
	/// </summary>
	public static double StopThreshold { get; set; } = 0.01;

	private double _velocity;
	private ScreenPosition _center;
	private DateTime _lastFrameTime;

	/// <summary>
	/// 現在のズーム速度
	/// </summary>
	public double Velocity => _velocity;

	/// <summary>
	/// ズームの中心位置
	/// </summary>
	public ScreenPosition Center => _center;

	/// <summary>
	/// 慣性アニメーションが動作中かどうか
	/// </summary>
	public bool IsRunning => Math.Abs(_velocity) >= StopThreshold;

	/// <summary>
	/// ホイールイベントを追加して速度を加算
	/// </summary>
	/// <param name="delta">ホイールのデルタ値（正: ズームイン、負: ズームアウト）</param>
	/// <param name="center">マウス位置（ズームの中心）</param>
	public void AddWheelEvent(double delta, ScreenPosition center)
	{
		_velocity += delta * VelocityPerStep;
		_center = center;

		// 初回またはリセット後は現在時刻を設定
		if (_lastFrameTime == default)
			_lastFrameTime = DateTime.Now;
	}

	/// <summary>
	/// トラッカーをリセット
	/// </summary>
	public void Reset()
	{
		_velocity = 0;
		_lastFrameTime = default;
	}

	/// <summary>
	/// フレームを処理してズーム変化量を取得
	/// </summary>
	/// <returns>このフレームでのズーム変化量。動作中でなければnull</returns>
	public double? ProcessFrame()
	{
		if (!IsRunning)
		{
			_velocity = 0;
			return null;
		}

		var now = DateTime.Now;
		var deltaTime = (now - _lastFrameTime).TotalSeconds;
		_lastFrameTime = now;

		// 異常な値を防ぐ
		if (deltaTime <= 0 || deltaTime > 0.1)
			deltaTime = 0.016;

		// 速度に基づいてズーム変化量を計算
		var zoomDelta = _velocity * deltaTime;

		// 速度を減衰
		_velocity *= Math.Exp(-DecayRate * deltaTime);

		return zoomDelta;
	}
}
