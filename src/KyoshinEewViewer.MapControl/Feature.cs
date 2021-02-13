using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System;
using System.Collections.Concurrent;
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
			Type = map.Arcs[index].Type switch
			{
				TopologyArcType.Coastline => FeatureType.Coastline,
				TopologyArcType.Admin => FeatureType.AdminBoundary,
				TopologyArcType.Area => FeatureType.AreaBoundary,
				_ => throw new NotImplementedException("未定義のTopologyArcTypeです"),
			};
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
			BB = new Rect(minLoc.CastPoint(), maxLoc.CastPoint());
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
			foreach (var i in PolyIndexes[0])
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
			BB = new Rect(minLoc.CastPoint(), maxLoc.CastPoint());

			Code = topologyPolygon.Code;
		}
		private Feature[] LineFeatures { get; }
		public Rect BB { get; }
		public bool IsClosed { get; }
		private Location[] Points { get; }
		private int[][] PolyIndexes { get; }
		public FeatureType Type { get; }

		public int? Code { get; }

		private ConcurrentDictionary<int, Point[][]> ReducedPointsCache { get; set; } = new();
		private Dictionary<int, Geometry> GeometryCache { get; set; } = new Dictionary<int, Geometry>();

		public Point[][] GetOrCreatePointsCache(MapProjection proj, int zoom)
		{
			if (ReducedPointsCache.ContainsKey(zoom))
				return ReducedPointsCache[zoom];

			if (Type != FeatureType.Polygon)
			{
				var p = Points.ToPixedAndRedction(proj, zoom, IsClosed);
				return ReducedPointsCache[zoom] = p == null ? null : new[] { p };
			}

			var pointsList = new List<List<Point>>();

			foreach (var polyIndex in PolyIndexes)
			{
				var points = new List<Point>();
				foreach (var i in polyIndex)
				{
					if (points.Count == 0)
					{
						if (i < 0)
						{
							var p = LineFeatures[Math.Abs(i) - 1].GetOrCreatePointsCache(proj, zoom);
							if (p != null)
								points.AddRange(p[0].Reverse());
						}
						else
						{
							var p = LineFeatures[i].GetOrCreatePointsCache(proj, zoom);
							if (p != null)
								points.AddRange(p[0]);
						}
						continue;
					}

					if (i < 0)
					{
						var p = LineFeatures[Math.Abs(i) - 1].GetOrCreatePointsCache(proj, zoom);
						if (p != null)
							points.AddRange(p[0].Reverse().Skip(1));
					}
					else
					{
						var p = LineFeatures[i].GetOrCreatePointsCache(proj, zoom);
						if (p != null)
							points.AddRange(p[0][1..]);
					}
				}
				if (points.Count > 0)
					pointsList.Add(points);
			}

			return ReducedPointsCache[zoom] = !pointsList.Any(p => p.Any()) ? null : pointsList.Select(p => p.ToArray()).ToArray();
		}
		public Geometry GetOrGenerateGeometry(MapProjection proj, int zoom)
		{
			if (GeometryCache.ContainsKey(zoom))
				return GeometryCache[zoom];
			var points = GetOrCreatePointsCache(proj, zoom);
			if (points == null)
				return GeometryCache[zoom] = null;

			var geometry = new StreamGeometry();
			using (var stream = geometry.Open())
				AddFigure(stream, proj, zoom);
			return GeometryCache[zoom] = geometry;
		}

		public void ClearCache()
		{
			ReducedPointsCache.Clear();
			GeometryCache.Clear();
		}

		public void AddFigure(StreamGeometryContext context, MapProjection proj, int zoom)
		{
			var pointsList = GetOrCreatePointsCache(proj, zoom);
			if (pointsList == null)
				return;
			foreach (var points in pointsList)
			{
				context.BeginFigure(points[0], Type == FeatureType.Polygon, IsClosed);
				foreach (var po in points[1..])
					context.LineTo(po, true, false);
			}
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
		AreaBoundary,
	}
}
