// Copyright (c) The Mapsui authors.
// SPDX-License-Identifier: MIT
// Original source: https://github.com/Mapsui/Mapsui

using System;

namespace KyoshinEewViewer.CustomControl.Manipulations;

/// <summary>
/// マルチタッチジェスチャーからパン・ズームの変化を計算するトラッカー
/// </summary>
public class ManipulationTracker
{
	private ManipulationState? _manipulationState;
	private ManipulationState? _previousManipulationState;
	private readonly ManipulationVelocityTracker _velocityTracker = new();

	/// <summary>
	/// 最初のManipulate呼び出し前に呼び出す
	/// </summary>
	public void Restart(ReadOnlySpan<ScreenPosition> positions) => Restart(GetManipulationState(positions));

	/// <summary>
	/// タッチ位置の変化からManipulationを計算する
	/// </summary>
	public void Manipulate(ReadOnlySpan<ScreenPosition> positions, Action<Manipulation> onManipulation)
		=> Manipulate(GetManipulationState(positions), onManipulation);

	private Manipulation? GetManipulation()
	{
		if (_manipulationState is null)
			return null;

		if (_previousManipulationState is null)
			return null;

		var scaleFactor = _manipulationState.GetScaleFactor(_previousManipulationState);

		if (_manipulationState.Equals(_previousManipulationState))
			return null;

		return new Manipulation(_manipulationState.Center, _previousManipulationState.Center, scaleFactor);
	}

	private static ManipulationState? GetManipulationState(ReadOnlySpan<ScreenPosition> positions)
	{
		if (positions.Length == 0)
			return null;

		if (positions.Length == 1)
			return new ManipulationState(positions[0], null, positions.Length);

		var (centerX, centerY) = GetCenter(positions);
		var radius = Distance(centerX, centerY, positions[0].X, positions[0].Y);

		return new ManipulationState(new ScreenPosition(centerX, centerY), radius, positions.Length);
	}

	private static double Distance(double x1, double y1, double x2, double y2)
		=> Math.Sqrt(Math.Pow(x1 - x2, 2.0) + Math.Pow(y1 - y2, 2.0));

	private static (double centerX, double centerY) GetCenter(ReadOnlySpan<ScreenPosition> positions)
	{
		double centerX = 0;
		double centerY = 0;

		foreach (var location in positions)
		{
			centerX += location.X;
			centerY += location.Y;
		}

		centerX /= positions.Length;
		centerY /= positions.Length;

		return (centerX, centerY);
	}

	private void Restart(ManipulationState? manipulationState)
	{
		_manipulationState = manipulationState;
		_previousManipulationState = null;
		// タッチ数が変わった場合は速度トラッカーもリセット
		_velocityTracker.Reset();
	}

	private void Manipulate(ManipulationState? manipulationState, Action<Manipulation> onManipulation)
	{
		_previousManipulationState = _manipulationState;
		_manipulationState = manipulationState;

		if (!(manipulationState?.LocationsLength == _previousManipulationState?.LocationsLength))
		{
			// タッチ数が変わった場合はリセット
			_previousManipulationState = null;
			_velocityTracker.Reset();
			return;
		}

		// 速度トラッカーにサンプルを追加
		if (manipulationState is not null)
			_velocityTracker.AddSample(manipulationState.Center, manipulationState.Radius, DateTime.Now.Ticks);

		var manipulation = GetManipulation();
		if (manipulation is not null)
			onManipulation(manipulation);
	}

	/// <summary>
	/// 慣性アニメーション用の速度を取得
	/// </summary>
	public ManipulationVelocity? GetInertiaVelocity() => _velocityTracker.GetInertiaVelocityIfNeeded();

	private record ManipulationState(ScreenPosition Center, double? Radius, int LocationsLength)
	{
		public double GetScaleFactor(ManipulationState previousManipulationState)
		{
			if (Radius is null)
				return 1;
			if (previousManipulationState.Radius is null)
				return 1;
			return Radius.Value / previousManipulationState.Radius.Value;
		}
	}
}

/// <summary>
/// マニピュレーションの結果
/// </summary>
/// <param name="Center">現在の中心位置</param>
/// <param name="PreviousCenter">前回の中心位置</param>
/// <param name="ScaleFactor">スケール係数（1.0 = 変化なし）</param>
public record Manipulation(ScreenPosition Center, ScreenPosition PreviousCenter, double ScaleFactor);
