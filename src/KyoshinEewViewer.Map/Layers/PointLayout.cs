using KyoshinEewViewer.Core.Models;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Map.Layers;

/// <summary>
/// ある配列・ズームにおける全点の絶対ピクセル座標と近傍探索を提供する。
/// イミュータブルのため、Render(コンポジタスレッド)とポインタイベント(UIスレッド)の間で安全に共有できる
/// </summary>
public sealed class PointLayout<TItem>
{
	private readonly NormalizedPointSet<TItem> _points;
	// ズームによるスケール係数(2^zoom)。構築時に1度だけ計算する
	private readonly double _scale;

	public TItem[] Source => _points.Source;
	public double Zoom { get; }
	/// <summary>全点の絶対ピクセル座標(LeftTopPixel 減算前)。座標が取得できない点は NaN</summary>
	public PointD[] Pixels { get; }

	internal PointLayout(NormalizedPointSet<TItem> points, double zoom)
	{
		_points = points;
		Zoom = zoom;
		_scale = Math.Pow(2, zoom);

		// 正規化座標にスケールを掛けるだけでピクセル座標を得る(Location.ToPixel と同一の計算結果になる)
		var normalized = points.Points;
		var pixels = new PointD[normalized.Length];
		for (var i = 0; i < normalized.Length; i++)
			pixels[i] = normalized[i] * _scale;
		Pixels = pixels;
	}

	/// <summary>
	/// 指定した絶対ピクセル座標から閾値未満の距離で最も近い点のインデックスを返す。該当なしの場合は null
	/// </summary>
	public int? FindNearest(PointD absolutePixel, double thresholdPixel)
		=> _points.FindNearest(absolutePixel / _scale, thresholdPixel / _scale);

	/// <summary>
	/// 指定した絶対ピクセル座標から閾値未満の距離にある点を(インデックス, ピクセル距離)で収集する。順序は不定
	/// </summary>
	public void CollectWithin(PointD absolutePixel, double thresholdPixel, List<(int Index, double Distance)> results)
	{
		// 探索は正規化座標空間で行い、距離のみピクセル単位へ換算して返す
		_points.CollectWithin(absolutePixel / _scale, thresholdPixel / _scale, results);
		for (var i = 0; i < results.Count; i++)
			results[i] = (results[i].Index, results[i].Distance * _scale);
	}
}
