using LibTessDotNet;
using SkiaSharp;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Map.Data;

public class PolygonFeature
{
	public static bool AsyncVerticeMode { get; set; } = true;
	public RectD BoundingBox { get; protected set; }
	public int MaxPoints { get; }
	public int? Code { get; protected set; }

	private TopologyMap Map { get; }

	public PolygonFeature(TopologyMap map, PolylineFeature[] lineFeatures, TopologyPolygon topologyPolygon)
	{
		Map = map;

		LineFeatures = lineFeatures;

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
	private ConcurrentDictionary<int, SKVertices?> PathCache { get; } = [];
	private ConcurrentDictionary<int, SKPath?> SKPathCache { get; } = [];

	public void ClearCache()
	{
		foreach (var p in SKPathCache.Values)
			p?.Dispose();
		SKPathCache.Clear();
		foreach (var p in PathCache.Values)
			p?.Dispose();
		PathCache.Clear();
	}

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

		return !pointsList.Any(p => p.Count != 0)
			? null
			: pointsList.Select(p => p.ToArray()).ToArray();
	}
	private bool IsWorking { get; set; } = false;
	private SKVertices? GetOrCreatePath(int zoom)
	{
		if (PathCache.TryGetValue(zoom, out var path))
			return path;

		if (IsWorking)
			return null;
		IsWorking = true;
		System.Threading.Tasks.Task.Run(() =>
		{
			try
			{
				var pointsList = CreatePointsCache(zoom);
				if (pointsList == null)
				{
					PathCache[zoom] = null;
					return;
				}

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
					points[i] = new(tess.Vertices[tess.Elements[i]].Position.X, tess.Vertices[tess.Elements[i]].Position.Y);
					points[i + 1] = new(tess.Vertices[tess.Elements[i + 1]].Position.X, tess.Vertices[tess.Elements[i + 1]].Position.Y);
					points[i + 2] = new(tess.Vertices[tess.Elements[i + 2]].Position.X, tess.Vertices[tess.Elements[i + 2]].Position.Y);
				}

				PathCache[zoom] = SKVertices.CreateCopy(SKVertexMode.Triangles, points, null, null);
			}
			finally
			{
				IsWorking = false;
				Map.OnAsyncObjectGenerated(zoom);
			}
		});
		return null;
	}
	private SKPath? GetOrCreateSKPath(int zoom)
	{
		if (SKPathCache.TryGetValue(zoom, out var path))
			return path;

		var pointsList = CreatePointsCache(zoom);
		if (pointsList == null)
			return SKPathCache[zoom] = null;

		path = new SKPath();

		foreach (var points in pointsList)
			path.AddPoly(points, true);

		return SKPathCache[zoom] = path;
	}

	public void Draw(SKCanvas canvas, int zoom, SKPaint paint)
	{
		if (AsyncVerticeMode)
		{
			if (GetOrCreatePath(zoom) is { } path)
			{
				canvas.DrawVertices(path, SKBlendMode.Modulate, paint);
				return;
			}

			if (IsWorking)
			{
				// 見つからなかった場合はより荒いポリゴンで描画できないか試みる
				if (zoom > 0 && PathCache.TryGetValue(zoom - 1, out path) && path != null)
				{
					canvas.Save();
					canvas.Scale(2);
					canvas.DrawVertices(path, SKBlendMode.Modulate, paint);
					canvas.Restore();
				}
				else if (PathCache.TryGetValue(zoom + 1, out path) && path != null)
				{
					canvas.Save();
					canvas.Scale(.5f);
					canvas.DrawVertices(path, SKBlendMode.Modulate, paint);
					canvas.Restore();
				}
			}
		}
		else
		{
			if (GetOrCreateSKPath(zoom) is not { } path)
				return;
			canvas.DrawPath(path, paint);
		}
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
