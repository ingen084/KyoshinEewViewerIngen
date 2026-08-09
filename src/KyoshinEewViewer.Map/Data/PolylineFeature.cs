using KyoshinEewViewer.Core.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
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
		Arc = arc.Arc ?? throw new Exception($"マップデータがうまく読み込めていません arc {index} がnullです");

		// IsClosed とバウンドボックスは一時バッファにデコードして求める(座標列は保持せず必要時にデコードする)
		var buffer = ArrayPool<Location>.Shared.Rent(Arc.Length);
		try
		{
			var length = Arc.ToLocationsInto(map, buffer);
			IsClosed =
				Math.Abs(buffer[0].Latitude - buffer[length - 1].Latitude) < 0.001 &&
				Math.Abs(buffer[0].Longitude - buffer[length - 1].Longitude) < 0.001;

			var minLoc = new Location(float.MaxValue, float.MaxValue);
			var maxLoc = new Location(float.MinValue, float.MinValue);
			for (var i = 0; i < length; i++)
			{
				var l = buffer[i];
				minLoc.Latitude = Math.Min(minLoc.Latitude, l.Latitude);
				minLoc.Longitude = Math.Min(minLoc.Longitude, l.Longitude);

				maxLoc.Latitude = Math.Max(maxLoc.Latitude, l.Latitude);
				maxLoc.Longitude = Math.Max(maxLoc.Longitude, l.Longitude);
			}
			BoundingBox = new RectD(minLoc.CastPoint(), maxLoc.CastPoint());
		}
		finally
		{
			ArrayPool<Location>.Shared.Return(buffer);
		}
	}

	~PolylineFeature()
	{
		ClearCache();
	}

	private IntVector[] Arc { get; }
	public PolylineType Type { get; }
	private Dictionary<int, SKPoint[]?> PointsCache { get; } = [];
	private Dictionary<int, SKPath> PathCache { get; } = []; //TODO nullable じゃない理由がわからない

	public void ClearCache()
	{
		FeatureLayer.ClearZoomCache(PathCache);
		FeatureLayer.ClearZoomCache(PointsCache);
	}

	/// <summary>
	/// 指定したズーム(±1)以外のキャッシュを解放する
	/// </summary>
	public void EvictCacheExcept(int[] activeZooms)
	{
		FeatureLayer.EvictZoomCacheExcept(PathCache, activeZooms);
		FeatureLayer.EvictZoomCacheExcept(PointsCache, activeZooms);
	}

	public SKPoint[]? GetPoints(int zoom)
	{
		Map.OnZoomUsed(zoom);
		lock (PointsCache)
			if (PointsCache.TryGetValue(zoom, out var points))
				return points;
		var computed = Arc.ToPixedAndReduction(Map, zoom, IsClosed);
		lock (PointsCache)
			PointsCache[zoom] = computed;
		return computed;
	}

	private bool IsWorking { get; set; }
	private bool IsPathCreationInProgress
	{
		get
		{
			lock (PathCache)
				return IsWorking;
		}
	}
	public SKPath? GetOrCreatePath(int zoom)
	{
		Map.OnZoomUsed(zoom);
		lock (PathCache)
		{
			if (PathCache.TryGetValue(zoom, out var path))
				return path;
			if (!AsyncMode)
				return CreatePath(zoom);
			if (IsWorking)
				return null;
			IsWorking = true;
		}

		// 非同期で生成する
		Task.Run(() =>
		{
			try
			{
				CreatePath(zoom);
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
	private SKPath? CreatePath(int zoom)
	{
		// 生成途中のパスが描画や解放の対象にならないよう、完成させてからキャッシュに公開する
		var path = new SKPath
		{
			// 穴開きポリゴンに対応させる
			FillType = SKPathFillType.EvenOdd,
		};

		var pointsList = GetPoints(zoom);
		if (pointsList == null)
		{
			lock (PathCache)
				PathCache[zoom] = path;
			return null;
		}
		path.AddPoly(pointsList, IsClosed);
		lock (PathCache)
			PathCache[zoom] = path;
		return path;
	}

	public void Draw(SKCanvas canvas, int zoom, SKPaint paint)
	{
		if (GetOrCreatePath(zoom) is { } path)
		{
			canvas.DrawPath(path, paint);
			return;
		}

		if (IsPathCreationInProgress)
		{
			// 見つからなかった場合はより荒いポリゴンで描画できないか試みる
			if (zoom > 0 && TryGetCachedPath(zoom - 1) is { } coarsePath)
			{
				canvas.Save();
				paint.StrokeWidth /= 2;
				canvas.Scale(2);
				canvas.DrawPath(coarsePath, paint);
				paint.StrokeWidth *= 2;
				canvas.Restore();
			}
			else if (TryGetCachedPath(zoom + 1) is { } finePath)
			{
				canvas.Save();
				paint.StrokeWidth *= 2;
				canvas.Scale(.5f);
				canvas.DrawPath(finePath, paint);
				paint.StrokeWidth /= 2;
				canvas.Restore();
			}
		}
	}

	private SKPath? TryGetCachedPath(int zoom)
	{
		lock (PathCache)
			return PathCache.TryGetValue(zoom, out var path) ? path : null;
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
