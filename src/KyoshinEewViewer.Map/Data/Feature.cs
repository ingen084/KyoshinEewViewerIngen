using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Map.Data;
#pragma warning disable CS8618 // TODO: nullable対応
public class Feature
{
	public Feature(TopologyMap map, int index)
	{
		var arc = map.Arcs?[index] ?? throw new Exception($"マップデータがうまく読み込めていません arc {index} が取得できませんでした");

		Type = arc.Type switch
		{
			TopologyArcType.Coastline => FeatureType.Coastline,
			TopologyArcType.Admin => FeatureType.AdminBoundary,
			TopologyArcType.Area => FeatureType.AreaBoundary,
			_ => throw new NotImplementedException("未定義のTopologyArcTypeです"),
		};
		;
		Points = arc.Arc?.ToLocations(map) ?? throw new Exception($"マップデータがうまく読み込めていません arc {index} がnullです");
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
		BB = new RectD(minLoc.CastPoint(), maxLoc.CastPoint());
	}
	public Feature(TopologyMap map, Feature[] lineFeatures, TopologyPolygon topologyPolygon)
	{
		Type = FeatureType.Polygon;
		LineFeatures = lineFeatures;
		IsClosed = true;

		var polyIndexes = topologyPolygon.Arcs ?? throw new Exception($"マップデータがうまく読み込めていません polygonのarcsがnullです");

		PolyIndexes = polyIndexes;

#pragma warning disable CS8602, CS8604 // 高速化のためチェックをサボる
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
				points.AddRange(map.Arcs[i].Arc.ToLocations(map).Skip(1));
		}
#pragma warning restore CS8602, CS8604

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
		BB = new RectD(minLoc.CastPoint(), maxLoc.CastPoint());

		Code = topologyPolygon.Code;
	}
	private Feature[] LineFeatures { get; }
	public RectD BB { get; }
	public bool IsClosed { get; }
	private Location[] Points { get; }
	private int[][] PolyIndexes { get; }
	public FeatureType Type { get; }

	public int? Code { get; }

	private Dictionary<int, SKPath> PathCache { get; } = new();

	private SKPoint[][]? GetOrCreatePointsCache(MapProjection proj, int zoom)
	{
		if (Type != FeatureType.Polygon)
		{
			var p = Points.ToPixedAndRedction(proj, zoom, IsClosed);
			return p == null ? null : new[] { p };
		}

		var pointsList = new List<List<SKPoint>>();

		foreach (var polyIndex in PolyIndexes)
		{
			var points = new List<SKPoint>();
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
						points.AddRange(p[0].Skip(1));
				}
			}
			if (points.Count > 0)
				pointsList.Add(points);
		}

		return !pointsList.Any(p => p.Any())
			? null
			: pointsList.Select(p => p.ToArray()).ToArray();
	}
	public SKPath? GetOrCreatePath(MapProjection proj, int zoom)
	{
		if (!PathCache.TryGetValue(zoom, out var path))
		{
			PathCache[zoom] = path = new SKPath();
			// 穴開きポリゴンに対応させる
			path.FillType = SKPathFillType.EvenOdd;

			var pointsList = GetOrCreatePointsCache(proj, zoom);
			if (pointsList == null)
				return null;
			for (var i = 0; i < pointsList.Length; i++)
				path.AddPoly(pointsList[i], Type == FeatureType.Polygon || IsClosed);
		}
		return path;
	}

	public void ClearCache()
	{
		foreach (var p in PathCache.Values)
			p.Dispose();
		PathCache.Clear();
	}

	public void Draw(SKCanvas canvas, MapProjection proj, int zoom, SKPaint paint)
	{
		if (GetOrCreatePath(proj, zoom) is not SKPath path)
			return;
		canvas.DrawPath(path, paint);
	}
	~Feature()
	{
		ClearCache();
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
