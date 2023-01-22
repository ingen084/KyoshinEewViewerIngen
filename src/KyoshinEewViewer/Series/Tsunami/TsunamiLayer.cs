using Avalonia.Controls;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Tsunami.Models;
using SkiaSharp;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.Tsunami;
public class TsunamiLayer : MapLayer
{
	private MapData? map;
	public MapData? Map
	{
		get => map;
		set {
			map = value;
			RefleshRequest();
		}
	}

	private TsunamiInfo? _current;
	public TsunamiInfo? Current
	{
		get => _current;
		set {
			if (_current == value) return;
			_current = value;
			RefleshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	private SKPaint MajorWarningPaint = new();
	private SKPaint WarningPaint = new();
	private SKPaint AdvisoryPaint = new();
	private SKPaint ForecastPaint = new();

	public override void RefreshResourceCache(Control targetControl)
	{
		MajorWarningPaint.Dispose();
		MajorWarningPaint = new()
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColors.Purple,
			IsAntialias = true,
			StrokeCap = SKStrokeCap.Square,
			StrokeJoin = SKStrokeJoin.Round,
		};

		WarningPaint.Dispose();
		WarningPaint = new()
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColors.Crimson,
			IsAntialias = true,
			StrokeCap = SKStrokeCap.Square,
			StrokeJoin = SKStrokeJoin.Round,
		};

		AdvisoryPaint.Dispose();
		AdvisoryPaint = new()
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColors.Gold,
			IsAntialias = true,
			StrokeCap = SKStrokeCap.Square,
			StrokeJoin = SKStrokeJoin.Round,
		};

		ForecastPaint.Dispose();
		ForecastPaint = new()
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColors.SkyBlue,
			IsAntialias = true,
			StrokeCap = SKStrokeCap.Square,
			StrokeJoin = SKStrokeJoin.Round,
		};
	}
	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Map == null)
			return;

		lock (Map)
		{
			if (Current == null || !Map.TryGetLayer(LandLayerType.TsunamiForecastArea, out var layer))
				return;

			canvas.Save();
			try
			{
				// 使用するキャッシュのズーム
				var baseZoom = (int)Math.Ceiling(param.Zoom);
				// 実際のズームに合わせるためのスケール
				var scale = Math.Pow(2, param.Zoom - baseZoom);
				canvas.Scale((float)scale);
				// 画面座標への変換
				var leftTop = param.LeftTopLocation.CastLocation().ToPixel(baseZoom);
				canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

				// スケールに合わせてブラシのサイズ変更
				MajorWarningPaint.StrokeWidth = (float)(10 / scale);
				WarningPaint.StrokeWidth = (float)(6 / scale);
				AdvisoryPaint.StrokeWidth = (float)(6 / scale);
				ForecastPaint.StrokeWidth = (float)(4 / scale);

				for (var i = 0; i < layer.PolyFeatures.Length; i++)
				{
					var f = layer.PolyFeatures[i];
					if (!param.ViewAreaRect.IntersectsWith(f.BB))
						continue;

					if (Current.MajorWarningAreas?.Any(a => a.Code == f.Code) ?? false)
						f.DrawAsPolyline(canvas, baseZoom, MajorWarningPaint);
					else if (Current.WarningAreas?.Any(a => a.Code == f.Code) ?? false)
						f.DrawAsPolyline(canvas, baseZoom, WarningPaint);
					else if (Current.AdvisoryAreas?.Any(a => a.Code == f.Code) ?? false)
						f.DrawAsPolyline(canvas, baseZoom, AdvisoryPaint);
					else if (Current.ForecastAreas?.Any(a => a.Code == f.Code) ?? false)
						f.DrawAsPolyline(canvas, baseZoom, ForecastPaint);
				}
			}
			finally
			{
				canvas.Restore();
			}
		}
	}
}
