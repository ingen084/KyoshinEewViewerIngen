// Copyright (c) The Mapsui authors.
// SPDX-License-Identifier: MIT
// Original source: https://github.com/Mapsui/Mapsui

using System;

namespace KyoshinEewViewer.CustomControl.Manipulations;

/// <summary>
/// 画面上の座標を表すレコード構造体
/// </summary>
public readonly record struct ScreenPosition(double X, double Y)
{
	public double Distance(ScreenPosition position)
		=> Math.Sqrt(Math.Pow(X - position.X, 2.0) + Math.Pow(Y - position.Y, 2.0));
}
