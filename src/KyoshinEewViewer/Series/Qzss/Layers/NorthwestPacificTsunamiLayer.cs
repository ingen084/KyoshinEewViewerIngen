using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Qzss.Converters;
using KyoshinEewViewer.Series.Qzss.Models;
using SkiaSharp;
using System;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Layers;

/// <summary>
/// 北西太平洋津波の予報地点を表示するレイヤー
/// </summary>
public class NorthwestPacificTsunamiLayer : MapLayer
{
	private NPTsunamiPoint[] _points = [];
	/// <summary>
	/// 表示する予報地点。地点が重なった場合に高いほうが隠れないよう、低いものから並べ替えて保持する
	/// </summary>
	public NPTsunamiPoint[] Points
	{
		get => _points;
		set
		{
			var sorted = value.OrderBy(p => GetHeightRank(p.Height)).ToArray();
			// 配列が更新されると添字の意味が変わるため、ホバー中の地点を地点コードで新配列から探し直す
			_hoverTracker.Rebind(_points, sorted);
			_points = sorted;
			RefreshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	// ホバー判定を行う画面ピクセル距離(マーカーがこれより小さい場合に使う最低値)
	private const double HoverThresholdPixel = 10;
	// マーカーと吹き出しの間隔
	private const float TooltipGap = 6;

	// 全点分の絶対ピクセル座標のキャッシュと近傍探索。配列参照+ズーム単位で再利用され、投影計算は配列差し替え時の1回で済む
	private readonly PointLayoutCache<NPTsunamiPoint> _pointLayoutCache = new(p => p.Location);
	// ホバー中の地点の管理。配列差し替え時は地点コードをキーとして新配列から探し直す
	private readonly HoverTracker<NPTsunamiPoint> _hoverTracker = new((a, b) => a.Area.Code == b.Area.Code);

	// SKPaintは全レイヤーインスタンスで共有する(コンポジタスレッドのRenderとRefreshResourceCacheの競合、
	// および電文グループごとのレイヤー生成によるリークを避けるため、Dispose・再生成はせず色プロパティの差し替えのみ行う)
	private static readonly SKPaint BorderPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 3,
		IsAntialias = true,
	};
	private static readonly SKPaint MajorWarningPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};
	private static readonly SKPaint WarningPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};
	private static readonly SKPaint AdvisoryPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};
	private static readonly SKPaint UnknownPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	/// <summary>
	/// 予想される津波の高さの深刻さ。同じ地点が複数回現れた場合の採用と、描画順に使用する
	/// </summary>
	public static int GetHeightRank(int height)
		=> height switch
		{
			509 => 6, // 巨大
			508 => 5, // 10m超
			4 => 4, // 5m~10m
			3 => 3, // 3m~5m
			2 or 510 => 2, // 1m~3m / 高い
			1 => 1, // 0.3m~1m
			_ => 0, // 不明
		};

	// 高さの区分は国内の津波警報･注意報の区分に合わせる
	private static SKPaint GetPaint(int height)
		=> height switch
		{
			3 or 4 or 508 or 509 => MajorWarningPaint,
			2 or 510 => WarningPaint,
			1 => AdvisoryPaint,
			_ => UnknownPaint,
		};

	/// <summary>
	/// ズームに応じたマーカーの半径
	/// </summary>
	private static float GetMarkerRadius(double zoom)
		=> (float)Math.Max(4, 8 + (zoom - 5) * 1.25);

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		BorderPaint.Color = windowTheme.IsDark ? SKColors.Black : SKColors.White;
		MajorWarningPaint.Color = SKColor.Parse(windowTheme.TsunamiMajorWarningColor);
		WarningPaint.Color = SKColor.Parse(windowTheme.TsunamiWarningColor);
		AdvisoryPaint.Color = SKColor.Parse(windowTheme.TsunamiAdvisoryColor);
		UnknownPaint.Color = SKColor.Parse(windowTheme.SubForegroundColor);
		MapLayerLabelRenderer.RefreshTheme(windowTheme.IsDark);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Points is not { Length: > 0 } points)
			return;

		// 全点分のピクセル座標のキャッシュを取得する(ズーム・配列が前回と同じ間は投影計算が発生しない)
		var pixels = _pointLayoutCache.Get(points, param.Zoom).Pixels;
		// UIスレッド側でのホバー変化と描画途中で値がずれないよう、最初に一度だけ読み取る
		var hoveredIndex = _hoverTracker.HoveredIndex;
		var radius = GetMarkerRadius(param.Zoom);

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			for (var i = 0; i < points.Length; i++)
				DrawMarker(canvas, points[i], pixels[i], radius);

			// ホバー中の吹き出しは他のマーカーより前面に表示するため最後に描画する
			if (hoveredIndex is { } hovered && hovered < points.Length)
			{
				var pixel = pixels[hovered];
				var (tooltipLines, tooltipRect) = ComputeTooltip(points[hovered], pixel, param, radius);

				// どの地点の吹き出しか分かるよう、ホバー中のマーカーを最前面に再描画してから引き出し線と吹き出しを描く
				DrawMarker(canvas, points[hovered], pixel, radius);
				// 引き出し線はマーカーと重ならないよう、マーカーの縁を始点にする
				if (MapLayerLabelRenderer.ComputeLeaderLine(pixel.AsSkPoint(), tooltipRect, radius) is { } leaderLine)
					canvas.DrawLine(leaderLine.Start, leaderLine.End, MapLayerLabelRenderer.LeaderLinePaint);

				MapLayerLabelRenderer.DrawTooltipBody(canvas, tooltipLines, tooltipRect);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	private static void DrawMarker(SKCanvas canvas, NPTsunamiPoint point, PointD pixel, float radius)
	{
		var center = pixel.AsSkPoint();
		canvas.DrawCircle(center, radius, BorderPaint);
		canvas.DrawCircle(center, radius, GetPaint(point.Height));
	}

	/// <summary>
	/// 吹き出しの表示内容と、画面端クランプまで適用した最終的な配置矩形を計算する。
	/// マーカーとの引き出し線はこのクランプ後の矩形を基準に描く必要があるため、計算と描画を分離している
	/// </summary>
	private static (string[] Lines, SKRect Rect) ComputeTooltip(NPTsunamiPoint point, PointD pixel, LayerRenderParameter param, float radius)
	{
		var lines = BuildTooltipLines(point);
		var (width, height) = MapLayerLabelRenderer.MeasureLines(lines);
		var center = pixel.AsSkPoint();
		var offset = radius + TooltipGap;

		// 既定ではマーカーの右に表示し、画面右端にかかる場合は左側に表示する
		var left = center.X + offset;
		if (left + width > param.PixelBound.Right)
			left = center.X - offset - width;

		// 左側の不透明パネル(Padding.Left)にかかる場合は、パネルの右側を最小左端としてクランプする
		var minLeft = (float)(param.PixelBound.Left + param.Padding.Left);
		if (left < minLeft)
			left = minLeft;

		// 画面の上端・下端にかかる場合は収まるように位置を調整する
		var top = center.Y - height / 2;
		if (top < param.PixelBound.Top)
			top = (float)param.PixelBound.Top;
		else if (top + height > param.PixelBound.Bottom)
			top = (float)(param.PixelBound.Bottom - height);

		return (lines, SKRect.Create(left, top, width, height));
	}

	private static string[] BuildTooltipLines(NPTsunamiPoint point)
		=> [
			DCReportConverters.GetNPTCoastalRegionText(point.Area.Code),
			DCReportConverters.GetNPTCoastalRegionAreaText(point.Area.Code),
			$"予想される高さ {point.Area.Height}",
			point.Area.Status,
		];

	public override bool OnPointerMoved(Location location, PointD screenPosition, LayerRenderParameter param)
	{
		if (Points is not { Length: > 0 } points)
		{
			if (_hoverTracker.SetHovered(null))
				RefreshRequest();
			return false;
		}

		// キャッシュ済みのピクセル座標から閾値未満で最も近いマーカーを探す
		var absolutePosition = screenPosition + param.LeftTopPixel;
		var nearestIndex = _pointLayoutCache.Get(points, param.Zoom).FindNearest(absolutePosition, Math.Max(HoverThresholdPixel, GetMarkerRadius(param.Zoom)));

		if (_hoverTracker.SetHovered(nearestIndex))
			RefreshRequest();
		return nearestIndex != null;
	}

	public override void OnPointerExited()
	{
		// ポインタが地図外に出た場合はホバー状態を解除する
		if (_hoverTracker.SetHovered(null))
			RefreshRequest();
	}
}
