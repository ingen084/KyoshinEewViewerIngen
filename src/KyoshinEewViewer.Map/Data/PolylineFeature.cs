using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Map.Data;
public class PolylineFeature
{
	public static bool AsyncMode { get; set; } = true;

	public RectD BoundingBox { get; protected set; }
	public bool IsClosed { get; protected set; }

	private TopologyMap Map { get; }

	public PolylineFeature(TopologyMap map, int index)
	{
		Map = map;

		var arc = map.Arcs?[index] ?? throw new Exception($"マップデータがうまく読み込めていません arc {index} が取得できませんでした");

		Type = arc.Type switch
		{
			TopologyArcType.Coastline => PolylineType.Coastline,
			TopologyArcType.Admin => PolylineType.AdminBoundary,
			TopologyArcType.Area => PolylineType.AreaBoundary,
			_ => throw new NotImplementedException("未定義のTopologyArcTypeです"),
		};
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
		BoundingBox = new RectD(minLoc.CastPoint(), maxLoc.CastPoint());
	}

	~PolylineFeature()
	{
		ClearCache();
	}

	private Location[] Points { get; }
	public PolylineType Type { get; }
	private ConcurrentDictionary<int, SKPoint[]?> PointsCache { get; } = [];
	private ConcurrentDictionary<int, SKPath> PathCache { get; } = []; //TODO nullable じゃない理由がわからない

	public void ClearCache()
	{
		foreach (var p in PathCache.Values)
			p.Dispose();
		PathCache.Clear();
		PointsCache.Clear();
	}

	public SKPoint[]? GetPoints(int zoom)
	{
		if (PointsCache.TryGetValue(zoom, out var points))
			return points;
		return PointsCache[zoom] = Points.ToPixedAndReduction(zoom, IsClosed);
	}

	private bool IsWorking { get; set; }
	public SKPath? GetOrCreatePath(int zoom)
	{
		if (PathCache.TryGetValue(zoom, out var path))
			return path;

		if (AsyncMode)
		{
			if (IsWorking)
				return null;
			IsWorking = true;
			// 非同期で生成する
			Task.Run(() =>
			{
				if (CreatePath(zoom) is { } p)
					Map.OnAsyncObjectGenerated(zoom);
				IsWorking = false;
			});
			return null;
		}
		else
			return CreatePath(zoom);
	}
	private SKPath? CreatePath(int zoom)
	{
		var path = PathCache[zoom] = new SKPath();
		// 穴開きポリゴンに対応させる
		path.FillType = SKPathFillType.EvenOdd;

		var pointsList = GetPoints(zoom);
		if (pointsList == null)
			return null;
		path.AddPoly(pointsList, IsClosed);
		return path;
	}

	public void Draw(SKCanvas canvas, int zoom, SKPaint paint)
	{
		if (GetOrCreatePath(zoom) is { } path)
		{
			canvas.DrawPath(path, paint);
			return;
		}

		if (IsWorking)
		{
			// 見つからなかった場合はより荒いポリゴンで描画できないか試みる
			if (zoom > 0 && PathCache.TryGetValue(zoom - 1, out path) && path != null)
			{
				canvas.Save();
				paint.StrokeWidth /= 2;
				canvas.Scale(2);
				canvas.DrawPath(path, paint);
				paint.StrokeWidth *= 2;
				canvas.Restore();
			}
			else if (PathCache.TryGetValue(zoom + 1, out path) && path != null)
			{
				canvas.Save();
				paint.StrokeWidth *= 2;
				canvas.Scale(.5f);
				canvas.DrawPath(path, paint);
				paint.StrokeWidth /= 2;
				canvas.Restore();
			}
		}
	}
}

public enum PolylineType
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
	/// サブ行政境界(市区町村)
	/// </summary>
	AreaBoundary,
}
