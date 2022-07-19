using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Data;
public class PolylineFeature : Feature
{
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
		BB = new RectD(minLoc.CastPoint(), maxLoc.CastPoint());
	}
	private Location[] Points { get; }
	public PolylineType Type { get; }

	public override SKPoint[][]? GetOrCreatePointsCache(int zoom)
	{
		var p = Points.ToPixedAndRedction(zoom, IsClosed);
		return p == null ? null : new[] { p };
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
				path.AddPoly(pointsList[i], IsClosed);
		}
		return path;
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
