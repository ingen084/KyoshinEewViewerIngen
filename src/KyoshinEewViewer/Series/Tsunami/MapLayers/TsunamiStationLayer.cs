using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Tsunami.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Tsunami.MapLayers;
public class TsunamiStationLayer : MapLayer
{
	private TsunamiInfo? _current;
	public TsunamiInfo? Current
	{
		get => _current;
		set {
			if (_current == value) return;
			_current = value;
			RefreshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme targetControl) { }

	private SKPaint NotObservationPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.Gray,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
	};
	private SKPaint ObservingPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.Gray.WithAlpha(220),
		Style = SKPaintStyle.Fill,
	};
	private SKPaint ObservedPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.Gold.WithAlpha(230),
		Style = SKPaintStyle.Fill,
	};
	private SKPaint LargeObservedPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.MediumPurple.WithAlpha(240),
		Style = SKPaintStyle.Fill,
	};
	private SKPaint TextPaint = new()
	{
		IsAntialias = true,
		Typeface = KyoshinEewViewerFonts.MainRegular,
		TextSize = 20,
		Color = SKColors.Black,
	};

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Current == null)
			return;

		canvas.Save();
		try
		{
			// 画面座標への変換
			var leftTop = param.LeftTopLocation.CastLocation().ToPixel(param.Zoom);
			canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

			void DrawStation(TsunamiObservationStation station)
			{
				if (station.Location == null)
					return;
				var loc = station.Location.ToPixel(param.Zoom);
				if (param.PixelBound.Left > loc.X || param.PixelBound.Right < loc.X ||
					param.PixelBound.Top > loc.Y || param.PixelBound.Bottom < loc.Y)
					return;
				switch (station.FirstHeightDetail)
				{
					case "ただちに来襲":
					case "津波到達中":
						canvas.DrawCircle((float)loc.X, (float)loc.Y, 5, ObservingPaint);
						break;
					case "第1波到達":
						if (station.MaxHeight is not null and >= 5)
							canvas.DrawCircle((float)loc.X, (float)loc.Y, 7, LargeObservedPaint);
						else
							canvas.DrawCircle((float)loc.X, (float)loc.Y, 6, ObservedPaint);
						break;
					default:
						canvas.DrawCircle((float)loc.X, (float)loc.Y, 3, NotObservationPaint);
						break;
				}
				if (param.Zoom >= 8)
					canvas.DrawText(station.Name, (float)loc.X + 5, (float)loc.Y + 5, TextPaint);
			}
			void DrawArea(TsunamiWarningArea[]? areas)
			{
				if (areas == null)
					return;
				foreach (var area in areas)
				{
					if (area.Stations == null)
						continue;
					foreach (var station in area.Stations)
						DrawStation(station);
				}
			}

			DrawArea(Current.ForecastAreas);
			DrawArea(Current.AdvisoryAreas);
			DrawArea(Current.WarningAreas);
			DrawArea(Current.MajorWarningAreas);
			DrawArea(Current.NoTsunamiAreas);
		}
		finally
		{
			canvas.Restore();
		}
	}
}
