using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Qzss.Converters;
using KyoshinEewViewer.Series.Qzss.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Layers;

/// <summary>
/// 台風の経路(実況・推定・予報)を地図上に表示するレイヤー
/// </summary>
public class TyphoonTrackLayer : MapLayer
{
	private TyphoonInformation[]? _informations;
	/// <summary>
	/// 表示する台風情報(経過時間昇順)
	/// </summary>
	public TyphoonInformation[]? Informations
	{
		get => _informations;
		set
		{
			// 配列が更新されると添字の意味が変わるため、ホバー中の点を内容キーで新配列から探し直す
			_hoverTracker.Rebind(_informations, value);
			_informations = value;
			// レイアウトキャッシュは配列参照の一致で有効性を判定しているため次回Renderで再構築されるが、
			// 参照だけ明示的に外しておくとOnPointerMovedが古い配列のヒット領域を参照する猶予を短くできる
			_trackLayout = null;
			RefreshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	// ホバー判定を行う画面ピクセル距離
	private const double HoverThresholdPixel = 10;
	// ラベル・吹き出し内の余白と行間
	private const float LabelPadding = 4;
	private const float LabelLineHeight = 15;
	// 実況点(×マーク)の半径サイズ
	private const float AnalysisMarkerSize = 7;
	// 推定・予報点(円マーク)の半径
	private const float CircleMarkerRadius = 6;
	// マーカーとラベルの間隔(既定位置=マーカー右のみに使う)
	private const float MarkerGap = 4;
	// 既定位置以外(左・上・下)に配置する場合の間隔。引き出し線の可視部分がはっきり確保されるよう広めにとる
	private const float OffsetLabelGap = 12;
	// 近傍4候補がすべて重なる場合に試す、より離れた候補の間隔
	private const float FarLabelGap = 28;
	// 引き出し線がこの長さ未満になる場合は描画しない(ラベルとマーカーがほぼ隣接しているとみなす)
	private const float LeaderLineMinLength = 1.5f;

	// 全点分の絶対ピクセル座標のキャッシュと近傍探索。配列参照+ズーム単位で再利用され、投影計算は配列差し替え時の1回で済む
	private readonly PointLayoutCache<TyphoonInformation> _pointLayoutCache = new(i => i.CenterLocation);
	// ホバー中の点の管理。配列差し替え時は種別・基準時刻・経過時間の組をキーとして新配列から探し直す
	private readonly HoverTracker<TyphoonInformation> _hoverTracker = new((a, b) => a.TimeType == b.TimeType && a.Time == b.Time && a.ElapsedHours == b.ElapsedHours);
	private bool _isDarkTheme;

	// ラベル文字の矩形上でもホバー判定できるようにするためのヒット領域
	private readonly record struct LabelHitArea(int Index, SKRect Rect);

	// ComputeNormalLabelの結果。CandidateIndexは採用された候補位置(0:右近傍/既定 1:左近傍 2:上近傍 3:下近傍
	// 4:右遠方 5:左遠方 6:上遠方 7:下遠方)で、既定位置(0)かどうかの判定に使う
	private readonly record struct NormalLabelPlacement(string[] Lines, SKRect Rect, int CandidateIndex);

	// 全点分のピクセル座標(PointLayout)・通常ラベル配置・引き出し線・ヒット領域をまとめたレイアウトのキャッシュ。
	// ホバー中の1点が切り替わるだけでは文字列生成やO(N^2)の重なり回避を再計算しないよう、
	// SourceとZoomが前回と同じ間はこれを再利用する(座標は絶対ピクセルのためパン中もキャッシュが有効)
	// Renderはコンポジタスレッド・OnPointerMovedはUIスレッドで走るため、構築完了後に参照を一括差し替えてロックなしに公開する
	private sealed record TrackLayout(
		PointLayout<TyphoonInformation> Points,
		NormalLabelPlacement[] Placements,
		(SKPoint Start, SKPoint End)?[] LeaderLines,
		LabelHitArea[] LabelHitAreas)
	{
		public TyphoonInformation[] Source => Points.Source;
		public PointD[] Pixels => Points.Pixels;
	}
	private TrackLayout? _trackLayout;

	// SKPaint/SKFontは全レイヤーインスタンスで共有する(コンポジタスレッドのRenderとRefreshResourceCacheの競合、
	// および台風ごとのレイヤー生成によるリークを避けるため、Dispose・再生成はせず色プロパティの差し替えのみ行う)
	private static readonly SKPaint TrackSolidPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Gray.WithAlpha(220),
		StrokeWidth = 3,
		IsAntialias = true,
	};
	private static readonly SKPaint TrackDashedPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Gray.WithAlpha(220),
		StrokeWidth = 3,
		IsAntialias = true,
		PathEffect = SKPathEffect.CreateDash([8, 6], 0),
	};
	private static readonly SKPaint AnalysisMarkerPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Crimson,
		StrokeWidth = 3,
		IsAntialias = true,
	};
	private static readonly SKPaint EstimateFillPaint = new()
	{
		Style = SKPaintStyle.Fill,
		Color = SKColors.Orange.WithAlpha(180),
		IsAntialias = true,
	};
	private static readonly SKPaint EstimateBorderPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.DarkOrange,
		StrokeWidth = 1,
		IsAntialias = true,
	};
	private static readonly SKPaint ForecastFillPaint = new()
	{
		Style = SKPaintStyle.Fill,
		Color = SKColors.CornflowerBlue.WithAlpha(160),
		IsAntialias = true,
	};
	private static readonly SKPaint ForecastBorderPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.SteelBlue,
		StrokeWidth = 1,
		IsAntialias = true,
	};
	// 通常ラベルは縁取り文字にしてテーマによらず視認性を確保する(色はRefreshResourceCacheで設定)
	private static readonly SKPaint LabelStrokePaint = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 3,
		IsAntialias = true,
	};
	private static readonly SKPaint LabelFillPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};
	// ラベルが既定位置から移動した際にマーカーとの対応を示す引き出し線。
	// 線自体が短いため縁取りは行わず、ラベル文字色の半透明単線で控えめに描画する(色はRefreshResourceCacheで設定)
	private static readonly SKPaint LeaderLinePaint = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1.2f,
		IsAntialias = true,
	};
	private static readonly SKPaint TooltipBackgroundPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};
	private static readonly SKPaint TooltipBorderPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 0.5f,
		IsAntialias = true,
	};
	private static readonly SKPaint TooltipTextPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	private static readonly SKFont LabelFont = new(KyoshinEewViewerFonts.MainRegular, 13)
	{
		Subpixel = true,
		Edging = SKFontEdging.SubpixelAntialias,
	};

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		_isDarkTheme = windowTheme.IsDark;

		// テーマ依存色はレイヤー間で共通のため、色プロパティのみ差し替える
		LabelStrokePaint.Color = _isDarkTheme ? SKColors.Black : SKColors.White;
		LabelFillPaint.Color = _isDarkTheme ? SKColors.White : SKColors.Black;
		LeaderLinePaint.Color = (_isDarkTheme ? SKColors.White : SKColors.Black).WithAlpha(140);

		// 吹き出しの配色はFluentAvaloniaのToolTip用リソースから取得し、アプリのテーマ切替に追従させる
		// (リソースが見つからない場合のみ従来のハードコード色にフォールバックする)
		var fallbackBackground = _isDarkTheme ? new SKColor(30, 30, 30) : new SKColor(255, 255, 255);
		var fallbackBorder = _isDarkTheme ? SKColors.LightGray : SKColors.DimGray;
		var fallbackText = _isDarkTheme ? SKColors.White : SKColors.Black;

		TooltipBackgroundPaint.Color = ResolveBrushColor("ToolTipBackgroundBrush", fallbackBackground).WithAlpha(230);
		TooltipBorderPaint.Color = ResolveBrushColor("ToolTipBorderBrush", fallbackBorder);
		TooltipTextPaint.Color = ResolveBrushColor("ToolTipForegroundBrush", fallbackText);
	}

	/// <summary>
	/// FluentAvaloniaのブラシリソースからSKColorを取得する。リソースが存在しない場合はフォールバック色を返す
	/// </summary>
	private static SKColor ResolveBrushColor(string resourceKey, SKColor fallback)
		=> KyoshinEewViewerApp.Application?.FindResource(resourceKey) is ISolidColorBrush brush
			? brush.Color.ToSKColor()
			: fallback;

	public override IReadOnlyDictionary<string, object?>? GetRenderInfo()
	{
		var count = Informations?.Length ?? 0;
		if (count == 0)
			return null;
		return new Dictionary<string, object?> { ["typhoonTrackPoints"] = count };
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Informations is not { Length: > 0 } informations)
			return;

		// ホバー1点の切り替えだけで全点分のラベル配置・文字列を再計算しないよう、元の配列参照とZoomが前回と同じ間はキャッシュを再利用する
		// (ズーム操作中はどのみち全レイヤーが再描画されるため、毎フレーム再構築になっても許容する)
		if (_trackLayout is not { } layout || !ReferenceEquals(layout.Source, informations) || layout.Points.Zoom != param.Zoom)
		{
			layout = BuildTrackLayout(_pointLayoutCache.Get(informations, param.Zoom));
			// OnPointerMovedはUIスレッドから参照するため、構築が完了してから参照を一括差し替える
			_trackLayout = layout;
		}

		// UIスレッド側でのホバー変化と描画途中で値がずれないよう、最初に一度だけ読み取る
		var hoveredIndex = _hoverTracker.HoveredIndex;

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			DrawTrack(canvas, layout);

			// 引き出し線→マーカー→ラベルの順で描画する(マーカーを後から重ねて描くことで、引き出し線がマーカー内部を通る部分は自然に隠れる)
			for (var i = 0; i < informations.Length; i++)
			{
				// ホバー中の点は通常ラベルの代わりに吹き出しで表示するため、ここでは引き出し線も描画しない
				if (i != hoveredIndex && layout.LeaderLines[i] is { } leaderLine)
					canvas.DrawLine(leaderLine.Start, leaderLine.End, LeaderLinePaint);
			}

			for (var i = 0; i < informations.Length; i++)
				DrawMarker(canvas, informations[i], layout.Pixels[i]);

			for (var i = 0; i < informations.Length; i++)
			{
				// ホバー中の点は通常ラベルの代わりに吹き出しで表示するため、ここでは描画しない
				if (i != hoveredIndex)
					DrawLabelLines(canvas, layout.Placements[i].Lines, layout.Placements[i].Rect, LabelStrokePaint, LabelFillPaint);
			}

			// ホバー中の吹き出しは他のラベルより前面に表示するため最後に描画する(1点分のみのため都度計算で構わない)
			if (hoveredIndex is { } hovered && hovered < informations.Length)
			{
				var hoveredInfo = informations[hovered];
				var hoveredPixel = layout.Pixels[hovered];
				var (tooltipLines, tooltipRect) = ComputeTooltip(hoveredInfo, hoveredPixel, param, layout.Placements[hovered].CandidateIndex);

				// どのマーカーの吹き出しか分かるよう引き出し線を描いてからマーカーを再描画し、
				// 線がマーカーの上を通る部分とツールチップ背景に隠れる部分が自然に見えるようにする
				if (ComputeLeaderLine(hoveredPixel.AsSkPoint(), tooltipRect) is { } leaderLine)
					canvas.DrawLine(leaderLine.Start, leaderLine.End, LeaderLinePaint);
				DrawMarker(canvas, hoveredInfo, hoveredPixel);

				DrawTooltipBody(canvas, tooltipLines, tooltipRect);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	/// <summary>
	/// 全点分の通常ラベル配置・引き出し線・ヒット領域を計算する(ピクセル座標は引数のPointLayoutが持つ)。
	/// 文字列生成やO(N^2)の重なり回避を含み負荷が高いため、Source・Zoomが変わらない間はRenderから再利用される
	/// </summary>
	private static TrackLayout BuildTrackLayout(PointLayout<TyphoonInformation> points)
	{
		var informations = points.Source;
		var pixels = points.Pixels;
		var count = informations.Length;

		// マーカー自体との重なりを避けるため、先に全マーカーの矩形を占有済みとして登録しておく
		// (各点でマーカー矩形1つ+ラベル矩形1つを積むため、初期容量はcountの2倍を見込む)
		var placedRects = new List<SKRect>(count * 2);
		foreach (var pixel in pixels)
		{
			var markerHalfSize = CircleMarkerRadius + 2;
			placedRects.Add(SKRect.Create(
				(float)pixel.X - markerHalfSize,
				(float)pixel.Y - markerHalfSize,
				markerHalfSize * 2,
				markerHalfSize * 2));
		}

		var placements = new NormalLabelPlacement[count];
		var leaderLines = new (SKPoint Start, SKPoint End)?[count];
		var labelHitAreas = new LabelHitArea[count];
		for (var i = 0; i < count; i++)
		{
			var placement = ComputeNormalLabel(informations[i], pixels[i], placedRects);
			placements[i] = placement;
			labelHitAreas[i] = new LabelHitArea(i, placement.Rect);
			placedRects.Add(placement.Rect);

			// 既定位置(マーカー右近傍)は引き出し線が不要なため、ここで確定させてRender側の分岐を単純にする
			leaderLines[i] = placement.CandidateIndex == 0
				? null
				: ComputeLeaderLine(pixels[i].AsSkPoint(), placement.Rect);
		}

		return new TrackLayout(points, placements, leaderLines, labelHitAreas);
	}

	private void DrawTrack(SKCanvas canvas, TrackLayout layout)
	{
		for (var i = 1; i < layout.Source.Length; i++)
		{
			// 行き先の点が予報の区間は破線、それ以外(実況・推定)は実線で結ぶ
			var paint = layout.Source[i].TimeType == ReferenceTimeType.Forecast ? TrackDashedPaint : TrackSolidPaint;
			canvas.DrawLine(layout.Pixels[i - 1].AsSkPoint(), layout.Pixels[i].AsSkPoint(), paint);
		}
	}

	private void DrawMarker(SKCanvas canvas, TyphoonInformation info, PointD pixel)
	{
		var center = pixel.AsSkPoint();

		if (info.TimeType == ReferenceTimeType.Analysis)
		{
			// 実況点は台風の中心位置を示す斜め十字(×)で描画する
			canvas.DrawLine(
				center + new SKPoint(-AnalysisMarkerSize, -AnalysisMarkerSize),
				center + new SKPoint(AnalysisMarkerSize, AnalysisMarkerSize),
				AnalysisMarkerPaint);
			canvas.DrawLine(
				center + new SKPoint(AnalysisMarkerSize, -AnalysisMarkerSize),
				center + new SKPoint(-AnalysisMarkerSize, AnalysisMarkerSize),
				AnalysisMarkerPaint);
			return;
		}

		var (fillPaint, borderPaint) = info.TimeType == ReferenceTimeType.Forecast
			? (ForecastFillPaint, ForecastBorderPaint)
			: (EstimateFillPaint, EstimateBorderPaint);
		canvas.DrawCircle(center, CircleMarkerRadius, fillPaint);
		canvas.DrawCircle(center, CircleMarkerRadius, borderPaint);
	}

	/// <summary>
	/// 通常ラベルの表示内容と配置矩形を計算する(ホバー中で描画しない場合もヒット領域として呼び出し元で記録するため、描画とは分離している)
	/// </summary>
	private static NormalLabelPlacement ComputeNormalLabel(TyphoonInformation info, PointD pixel, List<SKRect> placedRects)
	{
		var lines = BuildNormalLabelLines(info);
		var (width, height) = MeasureLines(lines);
		var center = pixel.AsSkPoint();

		// 右(既定)・左・上・下の順に近傍候補を試す
		var nearCandidates = BuildDirectionalCandidates(center, width, height, MarkerGap, OffsetLabelGap);
		for (var i = 0; i < nearCandidates.Length; i++)
		{
			if (placedRects.TrueForAll(p => !p.IntersectsWith(nearCandidates[i])))
				return new NormalLabelPlacement(lines, nearCandidates[i], i);
		}

		// 近傍candidatesが全て重なる場合は、同じ4方向でより離れた候補を試す
		var farCandidates = BuildDirectionalCandidates(center, width, height, FarLabelGap, FarLabelGap);
		for (var i = 0; i < farCandidates.Length; i++)
		{
			if (placedRects.TrueForAll(p => !p.IntersectsWith(farCandidates[i])))
				return new NormalLabelPlacement(lines, farCandidates[i], nearCandidates.Length + i);
		}

		// それでも配置できない場合は既定位置(右近傍)に重ねて配置する
		return new NormalLabelPlacement(lines, nearCandidates[0], 0);
	}

	/// <summary>
	/// マーカー中心から見て右→左→上→下の4方向の候補矩形を組み立てる。
	/// 右方向のみ<paramref name="rightGap"/>、他の3方向は<paramref name="otherGap"/>を間隔として使う
	/// (既定位置である右方向だけ従来通りの狭い間隔を維持するため)
	/// </summary>
	private static SKRect[] BuildDirectionalCandidates(SKPoint center, float width, float height, float rightGap, float otherGap)
	{
		var rightOffset = CircleMarkerRadius + rightGap;
		var otherOffset = CircleMarkerRadius + otherGap;
		return
		[
			SKRect.Create(center.X + rightOffset, center.Y - height / 2, width, height),
			SKRect.Create(center.X - otherOffset - width, center.Y - height / 2, width, height),
			SKRect.Create(center.X - width / 2, center.Y - otherOffset - height, width, height),
			SKRect.Create(center.X - width / 2, center.Y + otherOffset, width, height),
		];
	}

	/// <summary>
	/// マーカー中心とラベル(または吹き出し)矩形を結ぶ引き出し線の始点・終点を求める。線が短すぎる場合はnullを返す
	/// </summary>
	private static (SKPoint Start, SKPoint End)? ComputeLeaderLine(SKPoint center, SKRect rect)
	{
		// 起点はマーカー中心とする(この後にマーカーを描画するため、マーカー内部を通る部分は自然に隠れる)
		// 終点は矩形の外周でマーカーに最も近い点(矩形は凸形状のため、中心座標を矩形の範囲にクランプすれば求まる)
		var end = new SKPoint(
			Math.Clamp(center.X, rect.Left, rect.Right),
			Math.Clamp(center.Y, rect.Top, rect.Bottom));

		var dx = end.X - center.X;
		var dy = end.Y - center.Y;
		if (MathF.Sqrt(dx * dx + dy * dy) < LeaderLineMinLength)
			return null;

		return (center, end);
	}

	/// <summary>
	/// 通常ラベルの配置候補インデックス(0-7)から、吹き出しの展開方向に使う間隔(マーカー中心からの距離)を求める。
	/// 通常ラベルと同じ近傍/遠方の間隔を使うことで、ホバー時に吹き出しが不自然に離れて見えないようにする
	/// </summary>
	private static float TooltipGapFor(int candidateIndex) => candidateIndex switch
	{
		0 => MarkerGap, // 右・近傍(既定)
		4 => FarLabelGap, // 右・遠方
		1 or 2 or 3 => OffsetLabelGap, // 左・上・下(近傍)
		_ => FarLabelGap, // 左・上・下(遠方)
	};

	/// <summary>
	/// 通常ラベルの配置候補インデックスと同じ方向を第一候補として、吹き出しの左上位置を求める。
	/// 通常ラベルの矩形とアンカー辺を揃えるイメージで、右→左→上→下に対応する4方向のみを扱う
	/// </summary>
	private static (float Left, float Top) ComputeTooltipAnchor(int candidateIndex, SKPoint center, float width, float height)
	{
		var offset = CircleMarkerRadius + TooltipGapFor(candidateIndex);
		return (candidateIndex % 4) switch
		{
			1 => (center.X - offset - width, center.Y - height / 2), // 左
			2 => (center.X - width / 2, center.Y - offset - height), // 上
			3 => (center.X - width / 2, center.Y + offset), // 下
			_ => (center.X + offset, center.Y - height / 2), // 右(既定)
		};
	}

	/// <summary>
	/// ツールチップの表示内容と、画面端クランプまで適用した最終的な配置矩形を計算する。
	/// マーカーとの引き出し線はこのクランプ後の矩形を基準に描く必要があるため、計算と描画を分離している
	/// </summary>
	/// <param name="candidateIndex">ホバー中の点の通常ラベル配置候補インデックス。吹き出しの第一展開方向を揃えるために使う</param>
	private static (string[] Lines, SKRect Rect) ComputeTooltip(TyphoonInformation info, PointD pixel, LayerRenderParameter param, int candidateIndex)
	{
		var lines = BuildTooltipLines(info);
		var (width, height) = MeasureLines(lines);
		var center = pixel.AsSkPoint();

		var (left, top) = ComputeTooltipAnchor(candidateIndex, center, width, height);

		// 画面右端にかかる場合は左側に表示する
		if (left + width > param.PixelBound.Right)
			left = center.X - (CircleMarkerRadius + TooltipGapFor(candidateIndex)) - width;

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

	private static void DrawTooltipBody(SKCanvas canvas, string[] lines, SKRect rect)
	{
		canvas.DrawRoundRect(rect, 6, 6, TooltipBackgroundPaint);
		canvas.DrawRoundRect(rect, 6, 6, TooltipBorderPaint);
		DrawLabelLines(canvas, lines, rect, null, TooltipTextPaint);
	}

	public override bool OnPointerMoved(Location location, PointD screenPosition, LayerRenderParameter param)
	{
		if (Informations is not { Length: > 0 } informations)
		{
			if (_hoverTracker.SetHovered(null))
				RefreshRequest();
			return false;
		}

		// キャッシュ済みのピクセル座標から閾値未満で最も近いマーカーを探す
		var absolutePosition = screenPosition + param.LeftTopPixel;
		var nearestIndex = _pointLayoutCache.Get(informations, param.Zoom).FindNearest(absolutePosition, HoverThresholdPixel);

		// マーカーに当たらなかった場合、ラベル文字の矩形上にポインタがあるかも判定する
		// (Renderはコンポジタスレッド・ここはUIスレッドで走るため、記録時と同じ配列参照・Zoomの場合のみ絶対ピクセル座標で照合する)
		if (nearestIndex is null && _trackLayout is { } layout && layout.Points.Zoom == param.Zoom && ReferenceEquals(layout.Source, informations))
		{
			var absoluteSkPoint = absolutePosition.AsSkPoint();
			foreach (var area in layout.LabelHitAreas)
			{
				if (area.Index >= informations.Length || !area.Rect.Contains(absoluteSkPoint))
					continue;
				nearestIndex = area.Index;
				break;
			}
		}

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

	private static (float Width, float Height) MeasureLines(string[] lines)
	{
		var width = lines.Max(l => LabelFont.MeasureText(l));
		var height = lines.Length * LabelLineHeight;
		return (width + LabelPadding * 2, height + LabelPadding * 2);
	}

	/// <summary>
	/// ラベル(または吹き出し)の文字列を描画する
	/// </summary>
	/// <param name="strokePaint">縁取り用のペイント。null の場合は縁取りを描画しない</param>
	/// <param name="fillPaint">本体用のペイント</param>
	private static void DrawLabelLines(SKCanvas canvas, string[] lines, SKRect rect, SKPaint? strokePaint, SKPaint fillPaint)
	{
		var y = rect.Top + LabelPadding + LabelFont.Size;
		foreach (var line in lines)
		{
			var x = rect.Left + LabelPadding;
			if (strokePaint != null)
				canvas.DrawText(line, x, y, SKTextAlign.Left, LabelFont, strokePaint);
			canvas.DrawText(line, x, y, SKTextAlign.Left, LabelFont, fillPaint);
			y += LabelLineHeight;
		}
	}

	private static string TypeLabel(ReferenceTimeType type) => DCReportConverters.GetReferenceTimeTypeText(type);

	/// <summary>
	/// 大きさ・強さのカテゴリ文字列を組み立てる。両方とも値がない場合は null を返す
	/// </summary>
	private static string? BuildCategoryLine(byte scale, byte intensity)
	{
		var scaleText = DCReportConverters.GetTyphoonScaleText(scale);
		var intensityText = DCReportConverters.GetTyphoonIntensityText(intensity);
		return (scaleText, intensityText) switch
		{
			(null, null) => null,
			(not null, null) => scaleText,
			(null, not null) => intensityText,
			_ => $"{scaleText}で{intensityText}",
		};
	}

	private static string[] BuildNormalLabelLines(TyphoonInformation info)
	{
		var lines = new List<string> { $"{TypeLabel(info.TimeType)} {info.Time:d日 HH:mm}" };

		var detailParts = new List<string>();
		if (info.CentralPressure > 0)
			detailParts.Add($"{info.CentralPressure}hPa");
		if (info.MaxWindSpeed > 0)
			detailParts.Add($"最大 {info.MaxWindSpeed}m/s");
		if (detailParts.Count > 0)
			lines.Add(string.Join(" ", detailParts));

		if (BuildCategoryLine(info.Scale, info.Intensity) is { } categoryLine)
			lines.Add(categoryLine);

		return [.. lines];
	}

	private static string[] BuildTooltipLines(TyphoonInformation info)
	{
		var header = $"{info.Time:d日 HH:mm}";
		if (info.ElapsedHours > 0)
			header += $"({info.ElapsedHours}時間後)";
		header += $"の{TypeLabel(info.TimeType)}";

		var scaleText = DCReportConverters.GetTyphoonScaleText(info.Scale) ?? "-";
		var intensityText = DCReportConverters.GetTyphoonIntensityText(info.Intensity) ?? "-";

		return [
			header,
			$"中心気圧 {info.CentralPressure}hPa",
			$"最大風速 {(info.MaxWindSpeed > 0 ? $"{info.MaxWindSpeed}m/s" : "不明")}",
			$"最大瞬間風速 {(info.MaxWindGustSpeed > 0 ? $"{info.MaxWindGustSpeed}m/s" : "不明")}",
			$"大きさ {scaleText} / 強さ {intensityText}",
		];
	}
}
