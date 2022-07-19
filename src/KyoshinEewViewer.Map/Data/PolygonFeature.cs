using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Map.Data;
public class PolygonFeature : Feature
{
	public PolygonFeature(TopologyMap map, Feature[] lineFeatures, TopologyPolygon topologyPolygon)
	{
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
	private int[][] PolyIndexes { get; }

	public override SKPoint[][]? GetOrCreatePointsCache(int zoom)
	{
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
						var p = LineFeatures[Math.Abs(i) - 1].GetOrCreatePointsCache(zoom);
						if (p != null)
							points.AddRange(p[0].Reverse());
					}
					else
					{
						var p = LineFeatures[i].GetOrCreatePointsCache(zoom);
						if (p != null)
							points.AddRange(p[0]);
					}
					continue;
				}

				if (i < 0)
				{
					var p = LineFeatures[Math.Abs(i) - 1].GetOrCreatePointsCache(zoom);
					if (p != null)
						points.AddRange(p[0].Reverse().Skip(1));
				}
				else
				{
					var p = LineFeatures[i].GetOrCreatePointsCache(zoom);
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
	public override SKPath? GetOrCreatePath(int zoom)
	{
		if (!PathCache.TryGetValue(zoom, out var path))
		{
			PathCache[zoom] = path = new SKPath();
			// 穴開きポリゴンに対応させる
			path.FillType = SKPathFillType.EvenOdd;

			var pointsList = GetOrCreatePointsCache(zoom);
			if (pointsList == null)
				return null;
			for (var i = 0; i < pointsList.Length; i++)
				path.AddPoly(pointsList[i], true);
		}
		return path;
	}
}
