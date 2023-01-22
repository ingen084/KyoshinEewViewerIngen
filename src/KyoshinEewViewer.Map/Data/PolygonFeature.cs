using Avalonia;
using Avalonia.Controls;
using DynamicData;
using KyoshinMonitorLib;
using LibTessDotNet;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Map.Data;
public class PolygonFeature
{
	public RectD BB { get; protected set; }
	public bool IsClosed { get; protected set; }

	public int? Code { get; protected set; }

	public PolygonFeature(TopologyMap map, PolylineFeature[] lineFeatures, TopologyPolygon topologyPolygon)
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
	private PolylineFeature[] LineFeatures { get; }
	private int[][] PolyIndexes { get; }
	private Dictionary<int, SKPoint[]?> PathCache { get; } = new();

	public void ClearCache()
		=> PathCache.Clear();

	private SKPoint[][]? GetOrCreatePointsCache(int zoom)
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
	private SKPoint[]? GetOrCreatePath(int zoom)
	{
		if (PathCache.TryGetValue(zoom, out var path))
			return path;

		var pointsList = GetOrCreatePointsCache(zoom);
		if (pointsList == null)
			return PathCache[zoom] = null;

		var tess = new Tess();

		for (var i = 0; i < pointsList.Length; i++)
		{
			var vortexes = new ContourVertex[pointsList[i].Length];
			for (var j = 0; j < pointsList[i].Length; j++)
				vortexes[j].Position = new Vec3(pointsList[i][j].X, pointsList[i][j].Y, 0);
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
		return PathCache[zoom] = points;
	}

	public void Draw(SKCanvas canvas, int zoom, SKPaint paint)
	{
		if (GetOrCreatePath(zoom) is not SKPoint[] path)
			return;
		canvas.DrawVertices(SKVertexMode.Triangles, path, null, null, paint);
		//for (var i = 2; i < path.Length; i+=3)
		//{
		//	canvas.DrawLine(path[i], path[i - 1], paint);
		//	canvas.DrawLine(path[i], path[i - 2], paint);
		//	canvas.DrawLine(path[i - 1], path[i - 2], paint);
		//	//if (zoom >= 10)
		//	//	canvas.DrawText(i.ToString(), path[i], paint);
		//}
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
