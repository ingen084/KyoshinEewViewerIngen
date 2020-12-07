using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Media;

namespace LightningMapTest
{
	// 30秒で殺す
	public class LightningRealtimeRenderObject : RealtimeRenderObject
	{
		// 音の秒速
		private const double MachPerSecond = 1225000 / 60 / 60;
		private static Pen BorderPen;
		private static Pen CenterPen;
		private static Vector MarkerSize = new Vector(5, 5);

		private DateTime OccuranceTime { get; }
		private Location Location { get; }

		private double Distance { get; set; }
		private bool NeedUpdate { get; set; }

		public LightningRealtimeRenderObject(DateTime occuranceTime, DateTime nowDateTime, Location location)
		{
			if (BorderPen == null)
			{
				BorderPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1);
				BorderPen.Freeze();
				CenterPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)), 1);
				CenterPen.Freeze();
			}

			OccuranceTime = occuranceTime;
			BaseTime = nowDateTime;
			Location = location ?? throw new ArgumentNullException(nameof(location));
			NeedUpdate = true;
		}

		private Geometry cache;
		private double cachedZoom;
		
		public override void Render(DrawingContext context, Rect viewRect, double zoom, Point leftTopPixel, bool isDarkTheme)
		{
			var pointCenter = Location.ToPixel(zoom);
			if (!viewRect.IntersectsWith(new Rect(pointCenter - MarkerSize, pointCenter + MarkerSize)))
				return;

			var basePoint = (Point)(Location.ToPixel(zoom) - leftTopPixel);
			context.DrawLine(CenterPen, basePoint - new Vector(5, 5), basePoint + new Vector(5, 5));
			context.DrawLine(CenterPen, basePoint - new Vector(-5, 5), basePoint + new Vector(-5, 5));

			if (zoom <= 6)
				return;

			if (cache == null || NeedUpdate || cachedZoom != zoom)
			{
				cache = GeometryGenerator.MakeCircleGeometry(Location, Distance, zoom, 30);
				NeedUpdate = false;
				cachedZoom = zoom;
			}

			if (cache == null)
				return;
			if (cache.Transform is not TranslateTransform tt)
				cache.Transform = new TranslateTransform(-leftTopPixel.X, -leftTopPixel.Y);
			else
			{
				tt.X = -leftTopPixel.X;
				tt.Y = -leftTopPixel.Y;
			}
			context.DrawGeometry(null, BorderPen, cache);
		}

		protected override void OnTick()
		{
			var secs = (BaseTime + TimeOffset - OccuranceTime).TotalSeconds;
			Distance = secs * MachPerSecond;
			NeedUpdate = true;
		}
	}
}
