using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public static class Extensions
	{
		public static Point ToPixel(this Location loc, MapProjection projection, double zoom)
			=> projection.LatLngToPixel(loc, zoom);
		public static Location ToLocation(this Point loc, MapProjection projection, double zoom)
			=> projection.PixelToLatLng(loc, zoom);

		public static Point CastPoint(this Location loc)
			=> new Point(loc.Latitude, loc.Longitude);
		public static Location CastLocation(this Point loc)
			=> new Location((float)loc.X, (float)loc.Y);

		public static Location[] ToLocations(this IntVector[] points, TopologyMap map)
		{
			var result = new Location[points.Length];
			double x = 0;
			double y = 0;
			for (var i = 0; i < result.Length; i++)
				result[i] = new Location((float)((x += points[i].X) * map.Scale.X + map.Translate.X), (float)((y += points[i].Y) * map.Scale.Y + map.Translate.Y));
			return result;
		}


		public static Point[] ToPixedAndRedction(this Location[] nodes, MapProjection projection, double zoom, bool closed)
		{
			var points = DouglasPeucker.Reduction(nodes.Select(n => n.ToPixel(projection, zoom)).ToArray(), 1.5, closed);
			if (
				points.Length <= 1 ||
				(closed && points.Length <= 4)
			) // 小さなポリゴンは描画しない
				return null;
			return points;
		}
		public static PathFigure ToPolygonPathFigure(this Point[] points, bool closed)
			=> new PathFigure(points[0], points[1..].ToLineSegments(), closed);

		private static IEnumerable<PathSegment> ToLineSegments(this IEnumerable<Point> points)
			=> points.Select(pos => new LineSegment(pos, true));
	}
}
