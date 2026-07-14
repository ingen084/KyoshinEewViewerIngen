using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Styling;
using KyoshinEewViewer.Core;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.CustomControl;

/// <summary>
/// 地図レイヤー上のラベル・吹き出し(ツールチップ)・引き出し線描画の共通部品。
/// SKPaint/SKFontは全レイヤーで共有する(コンポジタスレッドのRenderとRefreshResourceCacheの競合、
/// およびレイヤー生成によるリークを避けるため、Dispose・再生成はせず色プロパティの差し替えのみ行う)
/// </summary>
public static class MapLayerLabelRenderer
{
	/// <summary>ラベル・吹き出し内の余白</summary>
	public const float LabelPadding = 4;
	/// <summary>ラベル・吹き出しの行間</summary>
	public const float LabelLineHeight = 15;
	// 引き出し線がこの長さ未満になる場合は描画しない(ラベルとマーカーがほぼ隣接しているとみなす)
	private const float LeaderLineMinLength = 1.5f;

	public static SKFont LabelFont { get; } = new(KyoshinEewViewerFonts.MainRegular, 13)
	{
		Subpixel = true,
		Edging = SKFontEdging.SubpixelAntialias,
	};

	// 通常ラベルは縁取り文字にしてテーマによらず視認性を確保する(色はRefreshThemeで設定)
	public static SKPaint LabelStrokePaint { get; } = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 3,
		IsAntialias = true,
	};
	public static SKPaint LabelFillPaint { get; } = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};
	// ラベルが既定位置から移動した際にマーカーとの対応を示す引き出し線。
	// 線自体が短いため縁取りは行わず、ラベル文字色の半透明単線で控えめに描画する(色はRefreshThemeで設定)
	public static SKPaint LeaderLinePaint { get; } = new()
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

	/// <summary>
	/// テーマ変更に合わせて共有ペイントの色を更新する。複数レイヤーから同じテーマで呼ばれても問題ない
	/// </summary>
	public static void RefreshTheme(bool isDarkTheme)
	{
		LabelStrokePaint.Color = isDarkTheme ? SKColors.Black : SKColors.White;
		LabelFillPaint.Color = isDarkTheme ? SKColors.White : SKColors.Black;
		LeaderLinePaint.Color = (isDarkTheme ? SKColors.White : SKColors.Black).WithAlpha(140);

		// 吹き出しの配色はFluentAvaloniaのToolTip用リソースから取得し、アプリのテーマ切替に追従させる
		// (リソースが見つからない場合のみ従来のハードコード色にフォールバックする)
		var fallbackBackground = isDarkTheme ? new SKColor(30, 30, 30) : new SKColor(255, 255, 255);
		var fallbackBorder = isDarkTheme ? SKColors.LightGray : SKColors.DimGray;
		var fallbackText = isDarkTheme ? SKColors.White : SKColors.Black;

		TooltipBackgroundPaint.Color = ResolveBrushColor("ToolTipBackgroundBrush", isDarkTheme, fallbackBackground).WithAlpha(230);
		TooltipBorderPaint.Color = ResolveBrushColor("ToolTipBorderBrush", isDarkTheme, fallbackBorder);
		TooltipTextPaint.Color = ResolveBrushColor("ToolTipForegroundBrush", isDarkTheme, fallbackText);
	}

	/// <summary>
	/// FluentAvaloniaのブラシリソースからSKColorを取得する。テーマバリアントを明示して解決するため、
	/// ダークテーマでもダーク用の配色が正しく引かれる。リソースが存在しない場合はフォールバック色を返す
	/// </summary>
	private static SKColor ResolveBrushColor(string resourceKey, bool isDarkTheme, SKColor fallback)
		=> Application.Current is { } app
			&& app.TryGetResource(resourceKey, isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light, out var value)
			&& value is ISolidColorBrush brush
			? brush.Color.ToSKColor()
			: fallback;

	/// <summary>
	/// ラベル(または吹き出し)の複数行文字列の描画サイズ(余白込み)を計測する
	/// </summary>
	public static (float Width, float Height) MeasureLines(string[] lines)
	{
		var width = 0f;
		foreach (var line in lines)
			width = Math.Max(width, LabelFont.MeasureText(line));
		var height = lines.Length * LabelLineHeight;
		return (width + LabelPadding * 2, height + LabelPadding * 2);
	}

	/// <summary>
	/// ラベル(または吹き出し)の文字列を描画する
	/// </summary>
	/// <param name="strokePaint">縁取り用のペイント。null の場合は縁取りを描画しない</param>
	/// <param name="fillPaint">本体用のペイント</param>
	public static void DrawLabelLines(SKCanvas canvas, string[] lines, SKRect rect, SKPaint? strokePaint, SKPaint fillPaint)
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

	/// <summary>
	/// 吹き出しの背景・枠・文字列を描画する
	/// </summary>
	public static void DrawTooltipBody(SKCanvas canvas, string[] lines, SKRect rect)
	{
		canvas.DrawRoundRect(rect, 6, 6, TooltipBackgroundPaint);
		canvas.DrawRoundRect(rect, 6, 6, TooltipBorderPaint);
		DrawLabelLines(canvas, lines, rect, null, TooltipTextPaint);
	}

	/// <summary>
	/// マーカー中心から見て右→左→上→下の4方向の候補矩形を組み立てる。
	/// 右方向のみ<paramref name="rightOffset"/>、他の3方向は<paramref name="otherOffset"/>をマーカー中心からの距離として使う
	/// (既定位置である右方向だけ狭い間隔を維持し、逃げた場合は引き出し線の可視部分を確保するため)
	/// </summary>
	public static SKRect[] BuildDirectionalCandidates(SKPoint center, float width, float height, float rightOffset, float otherOffset)
		=>
		[
			SKRect.Create(center.X + rightOffset, center.Y - height / 2, width, height),
			SKRect.Create(center.X - otherOffset - width, center.Y - height / 2, width, height),
			SKRect.Create(center.X - width / 2, center.Y - otherOffset - height, width, height),
			SKRect.Create(center.X - width / 2, center.Y + otherOffset, width, height),
		];

	/// <summary>
	/// マーカー中心とラベル(または吹き出し)矩形を結ぶ引き出し線の始点・終点を求める。線が短すぎる場合はnullを返す
	/// </summary>
	/// <param name="startOffset">始点をマーカー中心から矩形方向へずらす距離。マーカーを後から重ね描きしない場合にマーカー本体と線の重なりを避けるために使う</param>
	public static (SKPoint Start, SKPoint End)? ComputeLeaderLine(SKPoint center, SKRect rect, float startOffset = 0)
	{
		// 終点は矩形の外周でマーカーに最も近い点(矩形は凸形状のため、中心座標を矩形の範囲にクランプすれば求まる)
		var end = new SKPoint(
			Math.Clamp(center.X, rect.Left, rect.Right),
			Math.Clamp(center.Y, rect.Top, rect.Bottom));

		var dx = end.X - center.X;
		var dy = end.Y - center.Y;
		var length = MathF.Sqrt(dx * dx + dy * dy);
		if (length - startOffset < LeaderLineMinLength)
			return null;

		var start = startOffset > 0
			? new SKPoint(center.X + dx / length * startOffset, center.Y + dy / length * startOffset)
			: center;
		return (start, end);
	}
}
