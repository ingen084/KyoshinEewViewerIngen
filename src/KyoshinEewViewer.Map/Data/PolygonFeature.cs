using LibTessDotNet;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Map.Data;
public class PolygonFeature
{
	public RectD BoundingBox { get; protected set; }
	public bool IsClosed { get; protected set; }

	public int MaxPoints { get; }

	public int? Code { get; protected set; }

	public PolygonFeature(TopologyMap map, PolylineFeature[] lineFeatures, TopologyPolygon topologyPolygon)
	{
		LineFeatures = lineFeatures;
		IsClosed = true;

		var polyIndexes = topologyPolygon.Arcs ?? throw new Exception("マップデータがうまく読み込めていません polygonのarcsがnullです");

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

		MaxPoints = points.Count;
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
		BoundingBox = new RectD(minLoc.CastPoint(), maxLoc.CastPoint());

		Code = topologyPolygon.Code;
	}
	private PolylineFeature[] LineFeatures { get; }
	private int[][] PolyIndexes { get; }
	private Dictionary<int, SKPoint[]?> PathCache { get; } = new();

	public void ClearCache()
		=> PathCache.Clear();

	private SKPoint[][]? CreatePointsCache(int zoom)
	{
		var pointsList = new List<List<SKPoint>>();

		foreach (var polyIndex in PolyIndexes)
		{
			var points = new List<SKPoint>(MaxPoints);
			foreach (var i in polyIndex)
			{
				if (points.Count == 0)
				{
					if (i < 0)
					{
						var p = LineFeatures[Math.Abs(i) - 1].GetPoints(zoom);
						if (p != null)
							points.AddRange(p.Reverse());
					}
					else
					{
						var p = LineFeatures[i].GetPoints(zoom);
						if (p != null)
							points.AddRange(p);
					}
					continue;
				}

				if (i < 0)
				{
					var p = LineFeatures[Math.Abs(i) - 1].GetPoints(zoom);
					if (p != null)
						points.AddRange(p.Reverse().Skip(1));
				}
				else
				{
					var p = LineFeatures[i].GetPoints(zoom);
					if (p != null)
						points.AddRange(p.Skip(1));
				}
			}
			if (points.Count > 0)
				pointsList.Add(points);
		}

		return !pointsList.Any(p => p.Any())
			? null
			: pointsList.Select(p => p.ToArray()).ToArray();
	}
	public SKPoint[]? GetOrCreatePath(int zoom)
	{
		if (PathCache.TryGetValue(zoom, out var path))
			return path;

		var pointsList = CreatePointsCache(zoom);
		if (pointsList == null)
			return PathCache[zoom] = null;

		var tess = new Tess();

		foreach (var t in pointsList)
		{
			var vortexes = new ContourVertex[t.Length];
			for (var j = 0; j < t.Length; j++)
				vortexes[j].Position = new Vec3(t[j].X, t[j].Y, 0);
			tess.AddContour(vortexes, ContourOrientation.Original);
		}

		tess.Tessellate(WindingRule.Positive, ElementType.Polygons, 3);

		var points = new SKPoint[tess.ElementCount * 3];
		for (var i = 0; i < points.Length; i += 3)
		{
			points[i] = new SKPoint(tess.Vertices[tess.Elements[i]].Position.X, tess.Vertices[tess.Elements[i]].Position.Y);
			points[i + 1] = new SKPoint(tess.Vertices[tess.Elements[i + 1]].Position.X, tess.Vertices[tess.Elements[i + 1]].Position.Y);
			points[i + 2] = new SKPoint(tess.Vertices[tess.Elements[i + 2]].Position.X, tess.Vertices[tess.Elements[i + 2]].Position.Y);
		}
		return PathCache[zoom] = points;
	}

	public void Draw(SKCanvas canvas, int zoom, SKPaint paint)
	{
		if (GetOrCreatePath(zoom) is not { } path)
			return;
		canvas.DrawVertices(SKVertexMode.Triangles, path, null, null, paint);
	}
	public void DrawAsPolyline(SKCanvas canvas, int zoom, SKPaint paint)
	{
		foreach (var polyIndex in PolyIndexes)
		{
			foreach (var i in polyIndex)
			{
				if (i < 0)
					LineFeatures[Math.Abs(i) - 1].Draw(canvas, zoom, paint);
				else
					LineFeatures[i].Draw(canvas, zoom, paint);
			}
		}
	}
}
