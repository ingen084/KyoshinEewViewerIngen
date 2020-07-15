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
			Map = map;
			Points = Map.Arcs[index].ToLocations(Map);

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

			Type = map.Polygons.Count(p => p.Any(i => (i < 0 ? Math.Abs(i) - 1 : i) == index)) > 1 ? FeatureType.AdminBoundary : FeatureType.Coastline;
		}
		public Feature(TopologyMap map, int[] polyIndexes)
		{
			Map = map;

			var points = new List<Location>();
			foreach (var i in polyIndexes)
			{
				if (points.Count == 0)
				{
					if (i < 0)
						points.AddRange(Map.Arcs[Math.Abs(i) - 1].ToLocations(Map).Reverse());
					else
						points.AddRange(Map.Arcs[i].ToLocations(Map));
					continue;
				}

				if (i < 0)
					points.AddRange(Map.Arcs[Math.Abs(i) - 1].ToLocations(Map).Reverse().Skip(1));
				else
					points.AddRange(Map.Arcs[i].ToLocations(Map)[1..]);
			}

			Points = points.ToArray();

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

			Type = FeatureType.Polygon;
		}

		private TopologyMap Map { get; }
		public Rect BB { get; }
		private Location[] Points { get; }
		public FeatureType Type { get; }

		private Geometry GeometryCache { get; set; }
		private int CachedGeometryZoom { get; set; }

		public Geometry CreateGeometry(int zoom)
		{
			if (CachedGeometryZoom == zoom)
				return GeometryCache;
			CachedGeometryZoom = zoom;

			var figure = Points.ToPolygonPathFigure(zoom, 
				Math.Abs(Points[0].Latitude - Points[^1].Latitude) < 0.001 &&
				Math.Abs(Points[0].Longitude - Points[^1].Longitude) < 0.001);
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
