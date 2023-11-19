using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Map.Data;
public class PolylineFeature
{
	public RectD BoundingBox { get; protected set; }
	public bool IsClosed { get; protected set; }

	public PolylineFeature(TopologyMap map, int index)
	{
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
	private Dictionary<int, SKPoint[]?> PointsCache { get; } = [];
	private Dictionary<int, SKPath> PathCache { get; } = [];

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

	public SKPath? GetOrCreatePath(int zoom)
	{
		if (PathCache.TryGetValue(zoom, out var path))
			return path;

		PathCache[zoom] = path = new SKPath();
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
		if (GetOrCreatePath(zoom) is not { } path)
			return;
		canvas.DrawPath(path, paint);
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
