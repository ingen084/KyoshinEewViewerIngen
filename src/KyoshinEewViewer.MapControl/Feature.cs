using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public class Feature
	{
		public Feature(TopologyMap map, int index)
		{
			Type = map.Polygons.Count(p => p.Any(i => (i < 0 ? Math.Abs(i) - 1 : i) == index)) > 1 ? FeatureType.AdminBoundary : FeatureType.Coastline;
			Points = map.Arcs[index].ToLocations(map);
			IsClosed =
				Math.Abs(Points[0].Latitude - Points[^1].Latitude) < 0.001 &&
				Math.Abs(Points[0].Longitude - Points[^1].Longitude) < 0.001;

			// バウンドボックスを求める
			var minLoc = new Location(float.MaxValue, float.MaxValue);
			var maxLoc = new Location(float.MinValue, float.MinValue);
			foreach (var l in Points)
			{
				minLoc.Latitude = Math.Min(minLoc.Latitude, l.Latitude);
				minLoc.Longitude = Math.Min(minLoc.Longitude, l.Longitude);

				maxLoc.Latitude = Math.Max(maxLoc.Latitude, l.Latitude);
				maxLoc.Longitude = Math.Max(maxLoc.Longitude, l.Longitude);
			}
			BB = new Rect(minLoc.AsPoint(), maxLoc.AsPoint());
		}
		public Feature(TopologyMap map, Feature[] lineFeatures, int[] polyIndexes)
		{
			Type = FeatureType.Polygon;
			LineFeatures = lineFeatures;
			IsClosed = true;

			PolyIndexes = polyIndexes;

			// バウンドボックスを求めるために地理座標の計算をしておく
			var points = new List<Location>();
			foreach (var i in polyIndexes)
			{
				if (points.Count == 0)
				{
					if (i < 0)
						points.AddRange(map.Arcs[Math.Abs(i) - 1].ToLocations(map).Reverse());
					else
						points.AddRange(map.Arcs[i].ToLocations(map));
					continue;
				}

				if (i < 0)
					points.AddRange(map.Arcs[Math.Abs(i) - 1].ToLocations(map).Reverse().Skip(1));
				else
					points.AddRange(map.Arcs[i].ToLocations(map)[1..]);
			}
			// バウンドボックスを求める
			var minLoc = new Location(float.MaxValue, float.MaxValue);
			var maxLoc = new Location(float.MinValue, float.MinValue);
			foreach (var l in points)
			{
				minLoc.Latitude = Math.Min(minLoc.Latitude, l.Latitude);
				minLoc.Longitude = Math.Min(minLoc.Longitude, l.Longitude);

				maxLoc.Latitude = Math.Max(maxLoc.Latitude, l.Latitude);
				maxLoc.Longitude = Math.Max(maxLoc.Longitude, l.Longitude);
			}
			BB = new Rect(minLoc.AsPoint(), maxLoc.AsPoint());
		}
		private Feature[] LineFeatures { get; }
		public Rect BB { get; }
		public bool IsClosed { get; }
		private Location[] Points { get; }
		private int[] PolyIndexes { get; }
		public FeatureType Type { get; }

		private Point[] ReducedPoints { get; set; }
		private int ReducedPointsZoom { get; set; }
		private Geometry GeometryCache { get; set; }
		private int CachedGeometryZoom { get; set; }

		private Point[] CreatePointsCache(int zoom)
		{
			if (ReducedPointsZoom == zoom)
				return ReducedPoints;
			ReducedPointsZoom = zoom;

			if (Type == FeatureType.Polygon)
			{
				var points = new List<Point>();

				foreach (var i in PolyIndexes)
				{
					if (points.Count == 0)
					{
						if (i < 0)
						{
							var p = LineFeatures[Math.Abs(i) - 1].CreatePointsCache(zoom);
							if (p != null)
								points.AddRange(p.Reverse());
						}
						else
						{
							var p = LineFeatures[i].CreatePointsCache(zoom);
							if (p != null)
								points.AddRange(p);
						}
						continue;
					}

					if (i < 0)
					{
						var p = LineFeatures[Math.Abs(i) - 1].CreatePointsCache(zoom);
						if (p != null)
							points.AddRange(p.Reverse().Skip(1));
					}
					else
					{
						var p = LineFeatures[i].CreatePointsCache(zoom);
						if (p != null)
							points.AddRange(p[1..]);
					}
				}
				ReducedPoints = points.Count <= 0 ? null : points.ToArray();

				return ReducedPoints;
			}
			return ReducedPoints = Points.ToPixedAndRedction(zoom, IsClosed);
		}
		public Geometry CreateGeometry(int zoom)
		{
			if (CachedGeometryZoom == zoom)
				return GeometryCache;
			CreatePointsCache(zoom);
			CachedGeometryZoom = zoom;

			if (ReducedPoints == null)
				return null;
			var figure = ReducedPoints.ToPolygonPathFigure(IsClosed);

			if (figure == null)
			{
				GeometryCache = null;
				return null;
			}
			GeometryCache = new PathGeometry(new[] { figure });
			return GeometryCache;
		}
	}
	public enum FeatureType
	{
		/// <summary>
		/// 海岸線
		/// </summary>
		Coastline,
		/// <summary>
		/// 行政境界
		/// </summary>
		AdminBoundary,
		/// <summary>
		/// ポリゴン
		/// </summary>
		Polygon,
	}
}
