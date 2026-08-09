using KyoshinEewViewer.Core.Models;
using LibTessDotNet;
using SkiaSharp;
using System;
using System.Buffers;
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

	// 高速化のための逆順追加ヘルパーメソッド
	private static void AddRangeReversed<T>(List<T> target, IReadOnlyList<T> source, int skip = 0)
	{
		for (var i = source.Count - 1 - skip; i >= 0; i--)
			target.Add(source[i]);
	}

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
			var arcIndex = i < 0 ? Math.Abs(i) - 1 : i;
			var buffer = ArrayPool<Location>.Shared.Rent(map.Arcs[arcIndex].Arc.Length);
			try
			{
				// バッファに書き込んでもらう
				var length = map.Arcs[arcIndex].Arc.ToLocationsInto(map, buffer);

				if (points.Count == 0)
				{
					if (i < 0)
					{
						// 逆順で全て追加
						for (var j = length - 1; j >= 0; j--)
							points.Add(buffer[j]);
					}
					else
					{
						// 順方向で全て追加
						for (var j = 0; j < length; j++)
							points.Add(buffer[j]);
					}
				}
				else
				{
					if (i < 0)
					{
						// 逆順でskip=1
						for (var j = length - 2; j >= 0; j--)
							points.Add(buffer[j]);
					}
					else
					{
						// 順方向でskip=1
						for (var j = 1; j < length; j++)
							points.Add(buffer[j]);
					}
				}
			}
			finally
			{
				ArrayPool<Location>.Shared.Return(buffer);
			}
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
	private Dictionary<int, SKVertices?> PathCache { get; } = [];
	private Dictionary<int, SKPath?> SKPathCache { get; } = [];

	public void ClearCache()
	{
		FeatureLayer.ClearZoomCache(SKPathCache);
		FeatureLayer.ClearZoomCache(PathCache);
	}

	/// <summary>
	/// 指定したズーム(±1)以外のキャッシュを解放する
	/// </summary>
	public void EvictCacheExcept(int[] activeZooms)
	{
		FeatureLayer.EvictZoomCacheExcept(PathCache, activeZooms);
		FeatureLayer.EvictZoomCacheExcept(SKPathCache, activeZooms);
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
							AddRangeReversed(points, p);
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
						AddRangeReversed(points, p, skip: 1);
				}
				else
				{
					var p = LineFeatures[i].GetPoints(zoom);
					if (p != null)
						for (var j = 1; j < p.Length; j++)
							points.Add(p[j]);
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
	private bool IsPathCreationInProgress
	{
		get
		{
			lock (PathCache)
				return IsWorking;
		}
	}
	private SKVertices? GetOrCreatePath(int zoom)
	{
		Map.OnZoomUsed(zoom);
		lock (PathCache)
		{
			if (PathCache.TryGetValue(zoom, out var path))
				return path;
			if (IsWorking)
				return null;
			IsWorking = true;
		}
		System.Threading.Tasks.Task.Run(() =>
		{
			try
			{
				CreateVertices(zoom);
			}
			finally
			{
				lock (PathCache)
					IsWorking = false;
			}
			// 例外時にも通知するとキャッシュ未登録のまま再描画→再生成が無限に繰り返されるため、キャッシュを登録できた場合のみ通知する
			Map.OnAsyncObjectGenerated(zoom);
		});
		return null;
	}
	private void CreateVertices(int zoom)
	{
		var pointsList = CreatePointsCache(zoom);
		if (pointsList == null)
		{
			lock (PathCache)
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

		if (tess.VertexCount <= ushort.MaxValue)
		{
			// 頂点を共有するインデックス形式にしてネイティブメモリを節約する
			var positions = new SKPoint[tess.VertexCount];
			for (var i = 0; i < positions.Length; i++)
				positions[i] = new(tess.Vertices[i].Position.X, tess.Vertices[i].Position.Y);
			var indices = new ushort[tess.ElementCount * 3];
			for (var i = 0; i < indices.Length; i++)
				indices[i] = (ushort)tess.Elements[i];
			var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, null, null, indices);
			lock (PathCache)
				PathCache[zoom] = vertices;
		}
		else
		{
			// インデックス(ushort)で表現できない場合は展開形式にフォールバック
			var points = new SKPoint[tess.ElementCount * 3];
			for (var i = 0; i < points.Length; i += 3)
			{
				points[i] = new(tess.Vertices[tess.Elements[i]].Position.X, tess.Vertices[tess.Elements[i]].Position.Y);
				points[i + 1] = new(tess.Vertices[tess.Elements[i + 1]].Position.X, tess.Vertices[tess.Elements[i + 1]].Position.Y);
				points[i + 2] = new(tess.Vertices[tess.Elements[i + 2]].Position.X, tess.Vertices[tess.Elements[i + 2]].Position.Y);
			}

			var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, points, null, null);
			lock (PathCache)
				PathCache[zoom] = vertices;
		}
	}
	public SKPath? GetOrCreateSKPath(int zoom)
	{
		Map.OnZoomUsed(zoom);
		lock (SKPathCache)
			if (SKPathCache.TryGetValue(zoom, out var path))
				return path;

		var pointsList = CreatePointsCache(zoom);
		if (pointsList == null)
		{
			lock (SKPathCache)
				SKPathCache[zoom] = null;
			return null;
		}

		var newPath = new SKPath();

		foreach (var points in pointsList)
			newPath.AddPoly(points, true);

		lock (SKPathCache)
			SKPathCache[zoom] = newPath;
		return newPath;
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

			if (IsPathCreationInProgress)
			{
				// 見つからなかった場合はより荒いポリゴンで描画できないか試みる
				if (zoom > 0 && TryGetCachedVertices(zoom - 1) is { } coarseVertices)
				{
					canvas.Save();
					canvas.Scale(2);
					canvas.DrawVertices(coarseVertices, SKBlendMode.Modulate, paint);
					canvas.Restore();
				}
				else if (TryGetCachedVertices(zoom + 1) is { } fineVertices)
				{
					canvas.Save();
					canvas.Scale(.5f);
					canvas.DrawVertices(fineVertices, SKBlendMode.Modulate, paint);
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
	private SKVertices? TryGetCachedVertices(int zoom)
	{
		lock (PathCache)
			return PathCache.TryGetValue(zoom, out var vertices) ? vertices : null;
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
