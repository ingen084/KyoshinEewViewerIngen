using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.CustomControl.Manipulations;

/// <summary>
/// マニピュレーション（パン・ズーム）の速度を追跡するトラッカー
/// 各指の動きから中心位置とスケールの速度を計算
/// </summary>
public class ManipulationVelocityTracker
{
	private const int MaxSamples = 10;
	private const long MaxTicks = 80 * TimeSpan.TicksPerMillisecond;  // 過去80msのサンプルのみ使用

	private readonly Queue<ManipulationSample> _samples = [];

	/// <summary>
	/// 最大パン速度（ピクセル/秒）
	/// </summary>
	public static double MaxPanVelocity { get; set; } = 4000.0;

	/// <summary>
	/// 最小パン速度（ピクセル/秒）- この値以下では慣性を適用しない
	/// </summary>
	public static double MinPanVelocity { get; set; } = 50.0;

	/// <summary>
	/// 最大ズーム速度（スケール係数/秒）
	/// </summary>
	public static double MaxZoomVelocity { get; set; } = 3.0;

	/// <summary>
	/// 最小ズーム速度（スケール係数/秒）- この値以下では慣性を適用しない
	/// </summary>
	public static double MinZoomVelocity { get; set; } = 0.1;

	/// <summary>
	/// サンプルを追加
	/// </summary>
	public void AddSample(ScreenPosition center, double? radius, long ticks)
	{
		_samples.Enqueue(new ManipulationSample(center, radius, ticks));

		// 古いサンプルを削除
		while (_samples.Count > MaxSamples || (_samples.Count > 2 && _samples.Peek().Ticks < ticks - MaxTicks))
			_samples.Dequeue();
	}

	/// <summary>
	/// トラッカーをリセット
	/// </summary>
	public void Reset() => _samples.Clear();

	/// <summary>
	/// 現在の速度を計算
	/// </summary>
	public ManipulationVelocity CalculateVelocity(long now)
	{
		var samples = _samples.ToArray();
		if (samples.Length < 2)
			return new ManipulationVelocity(0, 0, 0, new ScreenPosition(0, 0));

		double totalDeltaX = 0;
		double totalDeltaY = 0;
		double totalScaleLog = 0;
		int validSampleCount = 0;

		var lastValidCenter = samples[^1].Center;

		for (var i = 1; i < samples.Length; i++)
		{
			var prev = samples[i - 1];
			var curr = samples[i];

			// 過去MaxTicks以内のサンプルのみ使用
			if (now - prev.Ticks > MaxTicks)
				continue;

			var deltaTime = (curr.Ticks - prev.Ticks) / (double)TimeSpan.TicksPerSecond; // 秒に変換
			if (deltaTime <= 0)
				continue;

			// パン速度
			totalDeltaX += curr.Center.X - prev.Center.X;
			totalDeltaY += curr.Center.Y - prev.Center.Y;

			// ズーム速度（対数スケール）
			if (prev.Radius.HasValue && curr.Radius.HasValue && prev.Radius.Value > 0)
			{
				var scaleRatio = curr.Radius.Value / prev.Radius.Value;
				totalScaleLog += Math.Log(scaleRatio);
			}

			validSampleCount++;
		}

		if (validSampleCount == 0 || samples.Length < 2)
			return new ManipulationVelocity(0, 0, 0, lastValidCenter);

		// 全体の経過時間から速度を計算
		var totalTime = (samples[^1].Ticks - samples[0].Ticks) / (double)TimeSpan.TicksPerSecond;
		if (totalTime <= 0)
			return new ManipulationVelocity(0, 0, 0, lastValidCenter);

		var panVelocityX = totalDeltaX / totalTime;
		var panVelocityY = totalDeltaY / totalTime;
		var zoomVelocity = totalScaleLog / totalTime;

		// 速度を制限
		var panVelocity = Math.Sqrt(panVelocityX * panVelocityX + panVelocityY * panVelocityY);
		if (panVelocity > MaxPanVelocity && panVelocity > 0)
		{
			var scale = MaxPanVelocity / panVelocity;
			panVelocityX *= scale;
			panVelocityY *= scale;
		}

		zoomVelocity = Math.Clamp(zoomVelocity, -MaxZoomVelocity, MaxZoomVelocity);

		return new ManipulationVelocity(panVelocityX, panVelocityY, zoomVelocity, lastValidCenter);
	}

	/// <summary>
	/// 慣性を適用すべきかどうかを判定し、必要であれば速度を返す
	/// </summary>
	public ManipulationVelocity? GetInertiaVelocityIfNeeded()
	{
		var velocity = CalculateVelocity(DateTime.Now.Ticks);

		var panSpeed = Math.Sqrt(velocity.PanVelocityX * velocity.PanVelocityX + velocity.PanVelocityY * velocity.PanVelocityY);
		var hasSignificantPan = panSpeed > MinPanVelocity;
		var hasSignificantZoom = Math.Abs(velocity.ZoomVelocity) > MinZoomVelocity;

		if (!hasSignificantPan && !hasSignificantZoom)
			return null;

		// 閾値以下の成分はゼロにする
		return new ManipulationVelocity(
			hasSignificantPan ? velocity.PanVelocityX : 0,
			hasSignificantPan ? velocity.PanVelocityY : 0,
			hasSignificantZoom ? velocity.ZoomVelocity : 0,
			velocity.LastCenter);
	}

	private record struct ManipulationSample(ScreenPosition Center, double? Radius, long Ticks);
}

/// <summary>
/// マニピュレーションの速度
/// </summary>
/// <param name="PanVelocityX">X方向パン速度（ピクセル/秒）</param>
/// <param name="PanVelocityY">Y方向パン速度（ピクセル/秒）</param>
/// <param name="ZoomVelocity">ズーム速度（対数スケール/秒）</param>
/// <param name="LastCenter">最後の中心位置（ズームの基準点として使用）</param>
public record ManipulationVelocity(double PanVelocityX, double PanVelocityY, double ZoomVelocity, ScreenPosition LastCenter);
