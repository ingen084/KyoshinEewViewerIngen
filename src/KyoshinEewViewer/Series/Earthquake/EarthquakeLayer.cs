using Avalonia.Controls;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeLayer : MapLayer
{
	public override bool NeedPersistentUpdate => false;

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

	public override void RefreshResourceCache(Control targetControl) { }

	private List<Location> Hypocenters { get; set; } = new();
	private Dictionary<JmaIntensity, List<(Location Location, string Name)>>? AreaItems { get; set; }
	private Dictionary<JmaIntensity, List<(Location Location, string Name)>>? CityItems { get; set; }
	private Dictionary<JmaIntensity, List<(Location Location, string Name)>>? StationItems { get; set; }

	public void UpdatePoints(
		List<Location> hypocenters,
		Dictionary<JmaIntensity, List<(Location, string)>>? areas,
		Dictionary<JmaIntensity, List<(Location, string)>>? cities,
		Dictionary<JmaIntensity, List<(Location, string)>>? stations
	)
	{
		Hypocenters = hypocenters;
		AreaItems = areas;
		CityItems = cities;
		StationItems = stations;
		RefleshRequest();
	}
	public void ClearPoints()
	{
		Hypocenters.Clear();
		AreaItems?.Clear();
		CityItems?.Clear();
		StationItems?.Clear();
		RefleshRequest();
	}

	public override void Render(SKCanvas canvas, bool isAnimating)
	{
		var useRoundIcon = Zoom >= 8;
		Dictionary<JmaIntensity, List<(Location Location, string Name)>>? renderItems = null;
		if (useRoundIcon)
			renderItems = StationItems ?? CityItems;
		else
			renderItems = AreaItems ?? CityItems ?? StationItems;
		if (renderItems == null)
			return;

		var circleSize = Zoom * 0.95;
		var circleVector = new PointD(circleSize, circleSize);

		var largeMinSize = 10 + (Zoom - 5) * 1.25;
		var largeMaxSize = largeMinSize + 1;
		var smallMinSize = 6 + (Zoom - 5) * 1.25;

		foreach (var hypo in Hypocenters)
		{
			HypocenterBodyPen.StrokeWidth = 6;
			var basePoint = hypo.ToPixel(Zoom) - LeftTopPixel;
			canvas.DrawLine((basePoint - new PointD(largeMaxSize, largeMaxSize)).AsSKPoint(), (basePoint + new PointD(largeMaxSize, largeMaxSize)).AsSKPoint(), HypocenterBorderPen);
			canvas.DrawLine((basePoint - new PointD(-largeMaxSize, largeMaxSize)).AsSKPoint(), (basePoint + new PointD(-largeMaxSize, largeMaxSize)).AsSKPoint(), HypocenterBorderPen);

			canvas.DrawLine((basePoint - new PointD(largeMinSize, largeMinSize)).AsSKPoint(), (basePoint + new PointD(largeMinSize, largeMinSize)).AsSKPoint(), HypocenterBodyPen);
			canvas.DrawLine((basePoint - new PointD(-largeMinSize, largeMinSize)).AsSKPoint(), (basePoint + new PointD(-largeMinSize, largeMinSize)).AsSKPoint(), HypocenterBodyPen);
		}

		foreach (var @int in renderItems.OrderBy(i => i.Key))
		{
			//using var iconCache = new SKBitmap((int)(circleSize * 2), (int)(circleSize * 2));
			//using (var cacheCanvas = new SKCanvas(iconCache))
			//{
			//	cacheCanvas.Clear(SKColors.Black);
			//	FixedObjectRenderer.DrawIntensity(
			//		cacheCanvas,
			//		@int.Key,
			//		new SKPoint(0, 0),
			//		(float)(circleSize * 2),
			//		false,
			//		useRoundIcon,
			//		false);
			//}

			foreach (var point in @int.Value)
			{
				var pointCenter = point.Location.ToPixel(Zoom);
				if (!PixelBound.IntersectsWith(new RectD(pointCenter - circleVector, pointCenter + circleVector)))
					continue;
				//canvas.DrawBitmap(iconCache, (pointCenter - circleVector - LeftTopPixel).AsSKPoint());
				FixedObjectRenderer.DrawIntensity(
					canvas,
					@int.Key,
					(pointCenter - LeftTopPixel).AsSKPoint(),
					(float)(circleSize * 2),
					true,
					useRoundIcon,
					false);
			}
		}

		foreach (var hypo in Hypocenters)
		{
			HypocenterBodyPen.StrokeWidth = 2;
			var basePoint = hypo.ToPixel(Zoom) - LeftTopPixel;
			canvas.DrawLine((basePoint - new PointD(smallMinSize, smallMinSize)).AsSKPoint(), (basePoint + new PointD(smallMinSize, smallMinSize)).AsSKPoint(), HypocenterBodyPen);
			canvas.DrawLine((basePoint - new PointD(-smallMinSize, smallMinSize)).AsSKPoint(), (basePoint + new PointD(-smallMinSize, smallMinSize)).AsSKPoint(), HypocenterBodyPen);
		}
	}
}
