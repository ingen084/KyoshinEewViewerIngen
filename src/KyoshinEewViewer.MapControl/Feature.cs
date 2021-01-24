using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public class Feature
	{
		public Feature(TopologyMap map, int index)
		{
			Type = map.Arcs[index].IsCoastline ? FeatureType.Coastline : FeatureType.AdminBoundary; //map.Polygons.Count(p => p.Arcs.Any(i => (i < 0 ? Math.Abs(i) - 1 : i) == index)) > 1 ? FeatureType.AdminBoundary : FeatureType.Coastline;
			Points = map.Arcs[index].Arc.ToLocations(map);
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
		public Feature(TopologyMap map, Feature[] lineFeatures, TopologyPolygon topologyPolygon)
		{
			Type = FeatureType.Polygon;
			LineFeatures = lineFeatures;
			IsClosed = true;

			var polyIndexes = topologyPolygon.Arcs;

			PolyIndexes = polyIndexes;

			// バウンドボックスを求めるために地理座標の計算をしておく
			var points = new List<Location>();
			foreach (var i in polyIndexes)
			{
				if (points.Count == 0)
				{
					if (i < 0)
						points.AddRange(map.Arcs[Math.Abs(i) - 1].Arc.ToLocations(map).Reverse());
					else
						points.AddRange(map.Arcs[i].Arc.ToLocations(map));
					continue;
				}

				if (i < 0)
					points.AddRange(map.Arcs[Math.Abs(i) - 1].Arc.ToLocations(map).Reverse().Skip(1));
				else
					points.AddRange(map.Arcs[i].Arc.ToLocations(map)[1..]);
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

			CountryCode = topologyPolygon.CountryCode;
			Prefecture = topologyPolygon.Prefecture;
		}
		private Feature[] LineFeatures { get; }
		public Rect BB { get; }
		public bool IsClosed { get; }
		private Location[] Points { get; }
		private int[] PolyIndexes { get; }
		public FeatureType Type { get; }

		public string CountryCode { get; }
		public string Prefecture { get; }

		private Dictionary<int, Point[]> ReducedPointsCache { get; set; } = new Dictionary<int, Point[]>();
		private Dictionary<int, Geometry> GeometryCache { get; set; } = new Dictionary<int, Geometry>();

		private Point[] CreatePointsCache(int zoom)
		{
			if (ReducedPointsCache.ContainsKey(zoom))
				return ReducedPointsCache[zoom];

			if (Type != FeatureType.Polygon)
				return ReducedPointsCache[zoom] = Points.ToPixedAndRedction(zoom, IsClosed);

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

			return ReducedPointsCache[zoom] = points.Count <= 0 ? null : points.ToArray();
		}
		public Geometry GetOrGenerateGeometry(int zoom)
		{
			if (GeometryCache.ContainsKey(zoom))
				return GeometryCache[zoom];
			CreatePointsCache(zoom);

			if (ReducedPointsCache[zoom] == null)
				return null;
			var figure = ReducedPointsCache[zoom].ToPolygonPathFigure(IsClosed);

			if (figure == null)
			{
				GeometryCache = null;
				return null;
			}
			return GeometryCache[zoom] = new PathGeometry(new[] { figure });
		}

		public void AddFigure(StreamGeometryContext context, int zoom)
		{
			CreatePointsCache(zoom);
			if (ReducedPointsCache[zoom] == null)
				return;
			context.BeginFigure(ReducedPointsCache[zoom][0], Type == FeatureType.Polygon, IsClosed);
			foreach (var po in ReducedPointsCache[zoom][1..])
				context.LineTo(po, true, false);
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
		/// <summary>
		/// サブ行政境界(市区町村)
		/// </summary>
		SubAdminBoundary,
	}
}
