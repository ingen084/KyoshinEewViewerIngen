using Avalonia.Controls;
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

	private SKPaint TextPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Typeface = FixedObjectRenderer.MainTypeface,
		TextSize = 14,
		StrokeWidth = 2,
	};

	private bool IsDarkTheme { get; set; }
	public override void RefreshResourceCache(Control targetControl)
	{
		bool FindBoolResource(string name)
			=> (bool)(targetControl.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
		IsDarkTheme = FindBoolResource("IsDarkTheme");
	}

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
		canvas.Save();
		try
		{
			var zoom = Zoom;
			canvas.Translate((float)-LeftTopPixel.X, (float)-LeftTopPixel.Y);
			var renderItemName = false;
			var useRoundIcon = false;
			Dictionary<JmaIntensity, List<(Location Location, string Name)>>? renderItems = null;
			if (zoom >= 8)
			{
				renderItemName = zoom >= 10;
				if (StationItems == null)
				{
					renderItems = CityItems;
				}
				else
				{
					renderItems = StationItems;
					useRoundIcon = true;
				}
			}
			else
			{
				if (AreaItems == null && CityItems == null)
				{
					renderItemName = zoom >= 10;
					renderItems = StationItems;
					useRoundIcon = true;
				}
				else
				{
					renderItemName = zoom >= 7.5;
					renderItems = AreaItems ?? CityItems;
				}
			}
			if (renderItems == null)
				return;

			var circleSize = Zoom * 0.95;
			var circleVector = new PointD(circleSize, circleSize);

			var largeMinSize = 10 + (zoom - 5) * 1.25;
			var largeMaxSize = largeMinSize + 1;
			var smallMinSize = 6 + (zoom - 5) * 1.25;

			foreach (var hypo in Hypocenters)
			{
				HypocenterBodyPen.StrokeWidth = 6;
				var basePoint = hypo.ToPixel(Zoom);
				canvas.DrawLine((basePoint - new PointD(largeMaxSize, largeMaxSize)).AsSKPoint(), (basePoint + new PointD(largeMaxSize, largeMaxSize)).AsSKPoint(), HypocenterBorderPen);
				canvas.DrawLine((basePoint - new PointD(-largeMaxSize, largeMaxSize)).AsSKPoint(), (basePoint + new PointD(-largeMaxSize, largeMaxSize)).AsSKPoint(), HypocenterBorderPen);

				canvas.DrawLine((basePoint - new PointD(largeMinSize, largeMinSize)).AsSKPoint(), (basePoint + new PointD(largeMinSize, largeMinSize)).AsSKPoint(), HypocenterBodyPen);
				canvas.DrawLine((basePoint - new PointD(-largeMinSize, largeMinSize)).AsSKPoint(), (basePoint + new PointD(-largeMinSize, largeMinSize)).AsSKPoint(), HypocenterBodyPen);
			}

			var fixedRect = new List<RectD>();

			foreach (var @int in renderItems.OrderBy(i => i.Key))
			{

				foreach (var point in @int.Value)
				{
					var pointCenter = point.Location.ToPixel(zoom);
					var bound = new RectD(pointCenter - circleVector, pointCenter + circleVector);
					if (!PixelBound.IntersectsWith(bound))
						continue;
					FixedObjectRenderer.DrawIntensity(
						canvas,
						@int.Key,
						pointCenter.AsSKPoint(),
						(float)(circleSize * 2),
						true,
						useRoundIcon,
						false);

					//fixedRect.Add(bound);
				}
			}

			foreach (var hypo in Hypocenters)
			{
				HypocenterBodyPen.StrokeWidth = 2;
				var basePoint = hypo.ToPixel(zoom);
				canvas.DrawLine((basePoint - new PointD(smallMinSize, smallMinSize)).AsSKPoint(), (basePoint + new PointD(smallMinSize, smallMinSize)).AsSKPoint(), HypocenterBodyPen);
				canvas.DrawLine((basePoint - new PointD(-smallMinSize, smallMinSize)).AsSKPoint(), (basePoint + new PointD(-smallMinSize, smallMinSize)).AsSKPoint(), HypocenterBodyPen);
			}


			if (!renderItemName)
				return;

			foreach (var @int in renderItems.OrderByDescending(i => i.Key))
			{
				foreach (var point in @int.Value)
				{
					var origCenterPoint = point.Location.ToPixel(zoom);
					var circleBound = new RectD(origCenterPoint - circleVector, origCenterPoint + circleVector);
					if (!PixelBound.IntersectsWith(circleBound))
						continue;
					var centerPoint = origCenterPoint;
					var text = point.Name;
					var textWidth = TextPaint.MeasureText(text);

					// デフォルトでは右側に
					var origBound = new RectD(
						centerPoint - new PointD(-circleVector.X - 2, TextPaint.TextSize * .5),
						centerPoint + new PointD(textWidth, TextPaint.TextSize * .4));
					var textBound = origBound;
					//var linkOrigin = origBound.BottomLeft + new PointD(1, 1);

					// 文字の被りチェック
					if (fixedRect.Any(r => r.IntersectsWith(textBound)))
					{
						continue;
						// 左側での描画を試す
						//var diffV = new PointD(textBound.Width + (circleSize + 2) * 3, 0);
						//textBound = new RectD(textBound.TopLeft - diffV, textBound.BottomRight - diffV);
						//centerPoint -= diffV;
						//linkOrigin = textBound.BottomRight + new PointD(-1, 1);

						//if (fixedRect.Any(r => r.IntersectsWith(textBound)))
						//{
						//	// 上側での描画を試す
						//	var diffV2 = new PointD(textBound.Width / 2 + (circleSize + 2), (circleSize + 2) * 2);
						//	textBound = new RectD(origBound.TopLeft - diffV2, origBound.BottomRight - diffV2);
						//	centerPoint = origCenterPoint - diffV2;
						//	linkOrigin = textBound.BottomRight - new PointD(textBound.Width / 2, -1);

						//	if (fixedRect.Any(r => r.IntersectsWith(textBound)))
						//	{
						//		// 下側での描画を試す
						//		var diffV3 = new PointD(textBound.Width / 2 + (circleSize + 2), -((circleSize + 1) * 2));
						//		textBound = new RectD(origBound.TopLeft - diffV3, origBound.BottomRight - diffV3);
						//		centerPoint = origCenterPoint - diffV3;
						//		linkOrigin = textBound.BottomRight - new PointD(textBound.Width / 2, 1);

						//		if (fixedRect.Any(r => r.IntersectsWith(textBound)))
						//			continue;
						//	}
						//}
					}
					fixedRect.Add(textBound);

					// 観測点情報文字の描画
					var textPoint = (centerPoint + new PointD(circleSize * 1.1, circleSize * .5)).AsSKPoint();

					TextPaint.Style = SKPaintStyle.Stroke;
					TextPaint.Color = !IsDarkTheme ? SKColors.White : SKColors.Black;
					canvas.DrawText(point.Name, textPoint, TextPaint);
					TextPaint.Style = SKPaintStyle.Fill;
					TextPaint.Color = IsDarkTheme ? SKColors.White : SKColors.Black;
					canvas.DrawText(point.Name, textPoint, TextPaint);
				}
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
