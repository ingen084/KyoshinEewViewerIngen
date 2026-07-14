using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Earthquake;

/// <summary>
/// 震源と観測点(地域・市区町村・観測点)ごとの震度を地図上に表示するレイヤー
/// </summary>
public class EarthquakeLayer : MapLayer
{
	public override bool NeedPersistentUpdate => false;

	// ホバー判定を行う画面ピクセル距離の下限(アイコンが小さい低ズームでも拾えるようにする)
	private const double HoverThresholdPixel = 10;
	// アイコンとラベルの間隔(既定位置=アイコン右のみに使う)
	private const float MarkerGap = 2;
	// 既定位置以外(左・上・下)に配置する場合の間隔。引き出し線の可視部分がはっきり確保されるよう広めにとる
	private const float OffsetLabelGap = 12;
	// アイコンとホバー中の吹き出しの間隔
	private const float TooltipGap = 4;

	private SKPaint HypocenterBorderPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 255, 0, 255),
		StrokeWidth = 8,
		IsAntialias = true,
	};

	private SKPaint HypocenterBodyPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 0, 0, 255),
		IsAntialias = true,
	};

	private SKPaint HypocenterRangePen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 0, 0, 255),
		IsAntialias = true,
	};

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		HypocenterBorderPen.Color = SKColor.Parse(windowTheme.EarthquakeHypocenterBorderColor);
		HypocenterBodyPen.Color = SKColor.Parse(windowTheme.EarthquakeHypocenterColor);

		var rangeColor = SKColor.Parse(windowTheme.EarthquakeHypocenterBorderColor);
		HypocenterRangePen.Color = rangeColor.WithAlpha(128);

		MapLayerLabelRenderer.RefreshTheme(windowTheme.IsDark);
	}

	/// <summary>震度アイコンとして描画する1点分の情報</summary>
	private readonly record struct IntensityPoint(JmaIntensity Intensity, Location Location, string Name);

	/// <summary>
	/// 震度アイコンの点集合。Pointsは震度昇順(=描画順)、LabelOrderは震度降順(=ラベルの場所取り優先順)の添字列
	/// </summary>
	private sealed record IntensityPointSet(IntensityPoint[] Points, int[] LabelOrder);

	/// <summary>
	/// 表示中の地震の全点データ。更新途中の状態をRender(コンポジタスレッド)が読まないよう、1オブジェクトで一括差し替えする
	/// </summary>
	private sealed record PointData(
		List<(Location Location, Location? Error)> Hypocenters,
		IntensityPointSet? Areas,
		IntensityPointSet? Cities,
		IntensityPointSet? Stations);
	private PointData? _data;

	// 全点分の絶対ピクセル座標のキャッシュと近傍探索。配列参照+ズーム単位で再利用され、投影計算は配列差し替え時の1回で済む
	private readonly PointLayoutCache<IntensityPoint> _pointLayoutCache = new(p => p.Location);
	// ホバー中の点の管理。配列差し替え時は名前・震度・座標の組をキーとして新配列から探し直す
	private readonly HoverTracker<IntensityPoint> _hoverTracker = new((a, b)
		=> a.Name == b.Name && a.Intensity == b.Intensity
		&& a.Location.Latitude == b.Location.Latitude && a.Location.Longitude == b.Location.Longitude);
	// ホバー中の点が属する配列。ズームにより地域・市区町村・観測点のどれが表示されているかが変わるため、添字と合わせて保持する
	private IntensityPoint[]? _hoveredSource;

	private readonly record struct LabelPlacement(string[] Lines, SKRect Rect, (SKPoint Start, SKPoint End)? LeaderLine);

	/// <summary>
	/// 画面内の点のラベル配置(重なり回避済み)とヒット領域のキャッシュ。
	/// 文字列計測とO(N^2)の重なり回避を含むため、Source・ズーム・表示範囲が前回と同じ間は再利用する
	/// (パン中は表示範囲が毎フレーム変わるため作り直しになるが、対象が画面内の点に限られるので許容する)。
	/// Renderはコンポジタスレッド・OnPointerMovedはUIスレッドで走るため、構築完了後に参照を一括差し替えてロックなしに公開する
	/// </summary>
	private sealed record LabelLayout(IntensityPoint[] Source, double Zoom, RectD PixelBound, LabelPlacement?[] Placements);
	private LabelLayout? _labelLayout;

	public void UpdatePoints(
		List<(Location Location, Location? Error)> hypocenters,
		Dictionary<JmaIntensity, List<(Location, string)>>? areas,
		Dictionary<JmaIntensity, List<(Location, string)>>? cities,
		Dictionary<JmaIntensity, List<(Location, string)>>? stations
	)
	{
		var data = new PointData(hypocenters, Flatten(areas), Flatten(cities), Flatten(stations));
		// 配列が差し替えられて添字の意味が変わるため、ホバー中の点を同じ種別の新配列から内容キーで探し直す
		RebindHover(_data, data);
		_data = data;
		_labelLayout = null;
		RefreshRequest();
	}

	public void ClearPoints()
	{
		_data = null;
		_labelLayout = null;
		SetHovered(null, null);
		RefreshRequest();
	}

	/// <summary>
	/// 震度ごとの辞書を震度昇順のフラット配列に変換し、あわせてラベルの場所取り優先順(震度降順・同震度内は元のリスト順)を組み立てる
	/// </summary>
	private static IntensityPointSet? Flatten(Dictionary<JmaIntensity, List<(Location Location, string Name)>>? source)
	{
		if (source == null)
			return null;

		var groups = source.OrderBy(g => g.Key).ToArray();
		var total = groups.Sum(g => g.Value.Count);
		var points = new IntensityPoint[total];
		var labelOrder = new int[total];

		var pointIndex = 0;
		foreach (var group in groups)
			foreach (var (location, name) in group.Value)
				points[pointIndex++] = new IntensityPoint(group.Key, location, name);

		// 末尾のグループ(最高震度)から順に、グループ内は元の順序(震央に近い順)のまま並べる
		var labelIndex = 0;
		var groupEnd = total;
		for (var g = groups.Length - 1; g >= 0; g--)
		{
			var groupStart = groupEnd - groups[g].Value.Count;
			for (var i = groupStart; i < groupEnd; i++)
				labelOrder[labelIndex++] = i;
			groupEnd = groupStart;
		}

		return new IntensityPointSet(points, labelOrder);
	}

	private void RebindHover(PointData? oldData, PointData? newData)
	{
		if (_hoveredSource is not { } hoveredSource)
			return;

		var newSource =
			ReferenceEquals(hoveredSource, oldData?.Areas?.Points) ? newData?.Areas?.Points :
			ReferenceEquals(hoveredSource, oldData?.Cities?.Points) ? newData?.Cities?.Points :
			ReferenceEquals(hoveredSource, oldData?.Stations?.Points) ? newData?.Stations?.Points : null;
		_hoverTracker.Rebind(hoveredSource, newSource);
		_hoveredSource = _hoverTracker.HoveredIndex is null ? null : newSource;
	}

	/// <summary>
	/// ズームに応じて表示する点集合と、名前ラベル・丸アイコンの使用有無を決定する
	/// </summary>
	private static (IntensityPointSet? Items, bool RenderItemName, bool UseRoundIcon) SelectRenderItems(PointData data, double zoom, bool isAnimating)
	{
		if (zoom >= 10 && data.Stations != null)
			return (data.Stations, !isAnimating, true);
		if (zoom >= 8.5 && data.Cities != null)
			return (data.Cities, !isAnimating && zoom >= 9.5, false);
		if (data.Areas == null && data.Cities == null)
			return (data.Stations, !isAnimating && zoom >= 10, true);
		return (data.Areas ?? data.Cities, !isAnimating && zoom >= 7.5, false);
	}

	public override IReadOnlyDictionary<string, object?>? GetRenderInfo()
	{
		var data = _data;
		var hypocenterCount = data?.Hypocenters.Count ?? 0;
		var iconCount = (data?.Areas?.Points.Length ?? 0) +
						(data?.Cities?.Points.Length ?? 0) +
						(data?.Stations?.Points.Length ?? 0);

		if (hypocenterCount == 0 && iconCount == 0)
			return null;

		var dict = new Dictionary<string, object?>();
		if (hypocenterCount > 0)
			dict["hypocenter"] = hypocenterCount;
		if (iconCount > 0)
			dict["icon"] = iconCount;

		return dict;
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (_data is not { } data)
			return;

		canvas.Save();
		try
		{
			var zoom = param.Zoom;
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			var (renderItems, renderItemName, useRoundIcon) = SelectRenderItems(data, zoom, isAnimating);
			if (renderItems == null)
				return;
			var points = renderItems.Points;
			// 全点分のピクセル座標のキャッシュを取得する(ズーム・配列が前回と同じ間は投影計算が発生しない)
			var layout = _pointLayoutCache.Get(points, zoom);
			var pixels = layout.Pixels;

			var circleSize = zoom * 0.95;
			var circleVector = new PointD(circleSize, circleSize);

			var largeMinSize = 10 + (zoom - 5) * 1.25;
			var largeMaxSize = largeMinSize + 1;
			var smallMinSize = 6 + (zoom - 5) * 1.25;

			foreach (var (hypo, _) in data.Hypocenters)
			{
				HypocenterBodyPen.StrokeWidth = 6;
				var basePoint = hypo.ToPixel(zoom);
				canvas.DrawLine((basePoint - new PointD(largeMaxSize, largeMaxSize)).AsSkPoint(), (basePoint + new PointD(largeMaxSize, largeMaxSize)).AsSkPoint(), HypocenterBorderPen);
				canvas.DrawLine((basePoint - new PointD(-largeMaxSize, largeMaxSize)).AsSkPoint(), (basePoint + new PointD(-largeMaxSize, largeMaxSize)).AsSkPoint(), HypocenterBorderPen);

				canvas.DrawLine((basePoint - new PointD(largeMinSize, largeMinSize)).AsSkPoint(), (basePoint + new PointD(largeMinSize, largeMinSize)).AsSkPoint(), HypocenterBodyPen);
				canvas.DrawLine((basePoint - new PointD(-largeMinSize, largeMinSize)).AsSkPoint(), (basePoint + new PointD(-largeMinSize, largeMinSize)).AsSkPoint(), HypocenterBodyPen);
			}

			// 震度昇順に並んでいるため、高い震度のアイコンが上に重なる
			for (var i = 0; i < points.Length; i++)
			{
				var pointCenter = pixels[i];
				var bound = new RectD(pointCenter - circleVector, pointCenter + circleVector);
				if (!param.PixelBound.IntersectsWith(bound))
					continue;
				FixedObjectRenderer.DrawIntensity(
					canvas,
					points[i].Intensity,
					pointCenter.AsSkPoint(),
					(float)(circleSize * 2),
					true,
					useRoundIcon,
					false,
					true,
					true);
			}

			foreach (var (hypo, _) in data.Hypocenters)
			{
				HypocenterBodyPen.StrokeWidth = 2;
				var basePoint = hypo.ToPixel(zoom);
				canvas.DrawLine((basePoint - new PointD(smallMinSize, smallMinSize)).AsSkPoint(), (basePoint + new PointD(smallMinSize, smallMinSize)).AsSkPoint(), HypocenterBodyPen);
				canvas.DrawLine((basePoint - new PointD(-smallMinSize, smallMinSize)).AsSkPoint(), (basePoint + new PointD(-smallMinSize, smallMinSize)).AsSkPoint(), HypocenterBodyPen);
			}

			if (zoom >= 8.75)
			{
				foreach (var (hypo, error) in data.Hypocenters)
				{
					// 誤差情報が無い場合は描画しない
					if (error is not { } err)
						continue;
					var topLeft = new Location(hypo.Latitude + err.Latitude, hypo.Longitude - err.Longitude).ToPixel(zoom);
					var bottomRight = new Location(hypo.Latitude - err.Latitude, hypo.Longitude + err.Longitude).ToPixel(zoom);
					var rect = new SKRect(
						(float)topLeft.X,
						(float)topLeft.Y,
						(float)bottomRight.X,
						(float)bottomRight.Y
					);

					var widthLength = rect.Width * 0.2f;
					var heightLength = rect.Height * 0.2f;

					// 左上
					canvas.DrawLine(rect.Left, rect.Top, rect.Left + widthLength, rect.Top, HypocenterRangePen);
					canvas.DrawLine(rect.Left, rect.Top, rect.Left, rect.Top + heightLength, HypocenterRangePen);
					// 右上
					canvas.DrawLine(rect.Right, rect.Top, rect.Right - widthLength, rect.Top, HypocenterRangePen);
					canvas.DrawLine(rect.Right, rect.Top, rect.Right, rect.Top + heightLength, HypocenterRangePen);
					// 左下
					canvas.DrawLine(rect.Left, rect.Bottom, rect.Left + widthLength, rect.Bottom, HypocenterRangePen);
					canvas.DrawLine(rect.Left, rect.Bottom, rect.Left, rect.Bottom - heightLength, HypocenterRangePen);
					// 右下
					canvas.DrawLine(rect.Right, rect.Bottom, rect.Right - widthLength, rect.Bottom, HypocenterRangePen);
					canvas.DrawLine(rect.Right, rect.Bottom, rect.Right, rect.Bottom - heightLength, HypocenterRangePen);
				}
			}

			// UIスレッド側でのホバー変化と描画途中で値がずれないよう、最初に一度だけ読み取る
			var hoveredIndex = ReferenceEquals(_hoveredSource, points) ? _hoverTracker.HoveredIndex : null;

			if (renderItemName && points.Length > 0)
			{
				// ホバー1点の切り替えだけで全点分のラベル配置を再計算しないよう、配列・ズーム・表示範囲が前回と同じ間はキャッシュを再利用する
				if (_labelLayout is not { } labelLayout || !ReferenceEquals(labelLayout.Source, points) || labelLayout.Zoom != zoom || !labelLayout.PixelBound.Equals(param.PixelBound))
				{
					labelLayout = BuildLabelLayout(renderItems, layout, param, (float)circleSize);
					// OnPointerMovedはUIスレッドから参照するため、構築が完了してから参照を一括差し替える
					_labelLayout = labelLayout;
				}

				foreach (var i in renderItems.LabelOrder)
				{
					// ホバー中の点は通常ラベルの代わりに吹き出しで表示するため、ここでは描画しない
					if (i == hoveredIndex || labelLayout.Placements[i] is not { } placement)
						continue;
					if (placement.LeaderLine is { } leaderLine)
						canvas.DrawLine(leaderLine.Start, leaderLine.End, MapLayerLabelRenderer.LeaderLinePaint);
					MapLayerLabelRenderer.DrawLabelLines(canvas, placement.Lines, placement.Rect, MapLayerLabelRenderer.LabelStrokePaint, MapLayerLabelRenderer.LabelFillPaint);
				}
			}

			// ホバー中の吹き出しは他のラベルより前面に表示するため最後に描画する(1点分のみのため都度計算で構わない)
			if (hoveredIndex is { } hovered && hovered < points.Length)
			{
				var item = points[hovered];
				var pixel = pixels[hovered];
				var (tooltipLines, tooltipRect) = ComputeTooltip(item, pixel, param, (float)circleSize);

				// どのアイコンの吹き出しか分かるよう、ホバー中のアイコンを最前面に再描画してから引き出し線と吹き出しを描く
				FixedObjectRenderer.DrawIntensity(
					canvas,
					item.Intensity,
					pixel.AsSkPoint(),
					(float)(circleSize * 2),
					true,
					useRoundIcon,
					false,
					true,
					true);
				// 引き出し線はアイコンと重ならないよう、アイコンの縁を始点にする
				if (MapLayerLabelRenderer.ComputeLeaderLine(pixel.AsSkPoint(), tooltipRect, (float)circleSize) is { } leaderLine)
					canvas.DrawLine(leaderLine.Start, leaderLine.End, MapLayerLabelRenderer.LeaderLinePaint);

				MapLayerLabelRenderer.DrawTooltipBody(canvas, tooltipLines, tooltipRect);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	/// <summary>
	/// 画面内の点のラベル配置を計算する。既定位置(アイコン右)が他のラベルと重なる場合は左・上・下へ逃がし、
	/// それでも重なる場合はラベルを描画しない(点が密集する状況では重ね書きするより読みやすいため)
	/// </summary>
	private static LabelLayout BuildLabelLayout(IntensityPointSet set, PointLayout<IntensityPoint> pointLayout, LayerRenderParameter param, float circleSize)
	{
		var points = set.Points;
		var pixels = pointLayout.Pixels;
		var placements = new LabelPlacement?[points.Length];
		var placedRects = new List<SKRect>(points.Length);
		var circleVector = new PointD(circleSize, circleSize);

		foreach (var i in set.LabelOrder)
		{
			var pixel = pixels[i];
			var circleBound = new RectD(pixel - circleVector, pixel + circleVector);
			if (!param.PixelBound.IntersectsWith(circleBound))
				continue;

			string[] lines = [points[i].Name];
			var (width, height) = MapLayerLabelRenderer.MeasureLines(lines);
			var center = pixel.AsSkPoint();

			// 右(既定)・左・上・下の順に候補を試す
			var candidates = MapLayerLabelRenderer.BuildDirectionalCandidates(center, width, height, circleSize + MarkerGap, circleSize + OffsetLabelGap);
			for (var c = 0; c < candidates.Length; c++)
			{
				var rect = candidates[c];
				if (!placedRects.TrueForAll(p => !p.IntersectsWith(rect)))
					continue;

				// 既定位置(アイコン右)以外に逃げた場合は、アイコンの縁を始点とする引き出し線で対応を示す
				placements[i] = new LabelPlacement(lines, rect,
					c == 0 ? null : MapLayerLabelRenderer.ComputeLeaderLine(center, rect, circleSize));
				placedRects.Add(rect);
				break;
			}
		}

		return new LabelLayout(points, pointLayout.Zoom, param.PixelBound, placements);
	}

	/// <summary>
	/// ツールチップの表示内容と、画面端クランプまで適用した最終的な配置矩形を計算する
	/// </summary>
	private static (string[] Lines, SKRect Rect) ComputeTooltip(IntensityPoint item, PointD pixel, LayerRenderParameter param, float circleSize)
	{
		var intensityText = item.Intensity == JmaIntensity.Unknown ? "震度不明" : item.Intensity.ToLongString();
		string[] lines = [item.Name, intensityText];
		var (width, height) = MapLayerLabelRenderer.MeasureLines(lines);
		var center = pixel.AsSkPoint();

		var left = center.X + circleSize + TooltipGap;
		var top = center.Y - height / 2;

		// 画面右端にかかる場合は左側に表示する
		if (left + width > param.PixelBound.Right)
			left = center.X - circleSize - TooltipGap - width;

		// 左側の不透明パネル(Padding.Left)にかかる場合は、パネルの右側を最小左端としてクランプする
		var minLeft = (float)(param.PixelBound.Left + param.Padding.Left);
		if (left < minLeft)
			left = minLeft;

		// 画面の上端・下端にかかる場合は収まるように位置を調整する
		if (top < param.PixelBound.Top)
			top = (float)param.PixelBound.Top;
		else if (top + height > param.PixelBound.Bottom)
			top = (float)(param.PixelBound.Bottom - height);

		return (lines, SKRect.Create(left, top, width, height));
	}

	public override bool OnPointerMoved(Location location, PointD screenPosition, LayerRenderParameter param)
	{
		if (_data is not { } data || SelectRenderItems(data, param.Zoom, false).Items is not { Points.Length: > 0 } renderItems)
		{
			if (SetHovered(null, null))
				RefreshRequest();
			return false;
		}

		// キャッシュ済みのピクセル座標から閾値未満で最も近いアイコンを探す
		var points = renderItems.Points;
		var absolutePosition = screenPosition + param.LeftTopPixel;
		var circleSize = param.Zoom * 0.95;
		var nearestIndex = _pointLayoutCache.Get(points, param.Zoom).FindNearest(absolutePosition, Math.Max(HoverThresholdPixel, circleSize));

		// アイコンに当たらなかった場合、ラベル文字の矩形上にポインタがあるかも判定する
		// (Renderはコンポジタスレッド・ここはUIスレッドで走るため、記録時と同じ配列参照・ズームの場合のみ絶対ピクセル座標で照合する)
		if (nearestIndex is null && _labelLayout is { } labelLayout && labelLayout.Zoom == param.Zoom && ReferenceEquals(labelLayout.Source, points))
		{
			var absoluteSkPoint = absolutePosition.AsSkPoint();
			for (var i = 0; i < labelLayout.Placements.Length; i++)
			{
				if (labelLayout.Placements[i] is not { } placement || !placement.Rect.Contains(absoluteSkPoint))
					continue;
				nearestIndex = i;
				break;
			}
		}

		if (SetHovered(nearestIndex is null ? null : points, nearestIndex))
			RefreshRequest();
		return nearestIndex != null;
	}

	public override void OnPointerExited()
	{
		// ポインタが地図外に出た場合はホバー状態を解除する
		if (SetHovered(null, null))
			RefreshRequest();
	}

	/// <summary>
	/// ホバー対象を設定し、変化した場合(=再描画が必要な場合)に true を返す
	/// </summary>
	private bool SetHovered(IntensityPoint[]? source, int? index)
	{
		var changed = _hoverTracker.SetHovered(index);
		if (!ReferenceEquals(_hoveredSource, source))
		{
			_hoveredSource = source;
			changed = true;
		}
		return changed;
	}
}
