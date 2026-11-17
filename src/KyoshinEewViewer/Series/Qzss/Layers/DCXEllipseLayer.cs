using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Layers;

/// <summary>
/// 地図に表示する災危通報(拡張) の楕円
/// </summary>
/// <param name="Ellipse">楕円</param>
/// <param name="Severity">深刻度 (対象地域の色に使用する)</param>
/// <param name="Direction">追加対象地域の場合は避難方向。対象地域の場合は null</param>
public record DCXEllipseItem(DcxEllipse Ellipse, AlertSeverity Severity, EvacuateDirectionType? Direction = null);

/// <summary>
/// 災危通報(拡張) の対象地域・追加対象地域の楕円を表示するレイヤー
/// </summary>
public class DCXEllipseLayer : MapLayer
{
	private (DCXEllipseItem Item, Location[] Points)[] _ellipses = [];
	/// <summary>
	/// 表示する楕円
	/// </summary>
	public DCXEllipseItem[] Ellipses
	{
		set {
			// 境界の点列は楕円ごとに一度だけ求めておく
			_ellipses = [.. value.Select(e => (e, e.Ellipse.ToPolygon().Select(p => new Location((float)p.Latitude, (float)p.Longitude)).ToArray()))];
			RefreshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	// SKPaint は全レイヤーインスタンスで共有し、色の差し替えのみ行う
	private static readonly SKPaint FillPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};
	private static readonly SKPaint BorderPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 2,
		IsAntialias = true,
	};
	private static readonly SKPaint DirectionBorderPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 3,
		IsAntialias = true,
		PathEffect = SKPathEffect.CreateDash([10, 6], 0),
	};

	private static SKColor _unknownColor;
	private static SKColor _moderateColor;
	private static SKColor _severeColor;
	private static SKColor _extremeColor;
	private static SKColor _leaveColor;
	// 向かうべき範囲は危険を示す色と区別できるよう固定の色にする
	private static readonly SKColor HeadColor = new(0x2E, 0xCC, 0x71);

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		_unknownColor = SKColor.Parse(windowTheme.WeatherWarningLevel2Color);
		_moderateColor = SKColor.Parse(windowTheme.WeatherWarningLevel3Color);
		_severeColor = SKColor.Parse(windowTheme.WeatherWarningLevel4Color);
		_extremeColor = SKColor.Parse(windowTheme.WeatherWarningLevel5Color);
		_leaveColor = SKColor.Parse(windowTheme.ForegroundColor);
	}

	private static SKColor GetSeverityColor(AlertSeverity severity) => severity switch
	{
		AlertSeverity.Moderate => _moderateColor,
		AlertSeverity.Severe => _severeColor,
		AlertSeverity.Extreme => _extremeColor,
		_ => _unknownColor,
	};

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (_ellipses is not { Length: > 0 } ellipses)
			return;

		using var path = new SKPath();
		foreach (var (item, points) in ellipses)
		{
			path.Reset();
			for (var i = 0; i < points.Length; i++)
			{
				var pixel = (points[i].ToPixel(param.Zoom) - param.LeftTopPixel).AsSkPoint();
				if (i == 0)
					path.MoveTo(pixel);
				else
					path.LineTo(pixel);
			}
			path.Close();

			if (item.Direction is { } direction)
			{
				DirectionBorderPaint.Color = direction == EvacuateDirectionType.Head ? HeadColor : _leaveColor;
				canvas.DrawPath(path, DirectionBorderPaint);
				continue;
			}

			var color = GetSeverityColor(item.Severity);
			FillPaint.Color = color.WithAlpha(0x40);
			canvas.DrawPath(path, FillPaint);
			BorderPaint.Color = color;
			canvas.DrawPath(path, BorderPaint);
		}
	}
}
