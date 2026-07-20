using KyoshinEewViewer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Map.Data;

public class FeatureLayer
{
	public TopologyMap BasedMap { get; }

	public PolylineFeature[] LineFeatures { get; }
	public PolylineFeature[] CoastlineFeatures { get; }
	public PolylineFeature[] AdminBoundaryFeatures { get; }
	public PolylineFeature[] AreaBoundaryFeatures { get; }
	public PolygonFeature[] PolyFeatures { get; }

	public FeatureLayer(TopologyMap map)
	{
		// タイプ別
		var coastlines = new List<PolylineFeature>();
		var adminBoundaries = new List<PolylineFeature>();
		var areaBoundaries = new List<PolylineFeature>();

		LineFeatures = new PolylineFeature[map.Arcs?.Length ?? 0];
		if (map.Arcs != null)
			for (var i = 0; i < map.Arcs.Length; i++)
			{
				var f = new PolylineFeature(map, i);
				LineFeatures[i] = f;
				switch (f.Type)
				{
					case PolylineType.Coastline:
						coastlines.Add(f);
						break;
					case PolylineType.AdminBoundary:
						adminBoundaries.Add(f);
						break;
					case PolylineType.AreaBoundary:
						areaBoundaries.Add(f);
						break;
				}
			}

		CoastlineFeatures = coastlines.ToArray();
		AdminBoundaryFeatures = adminBoundaries.ToArray();
		AreaBoundaryFeatures = areaBoundaries.ToArray();

		PolyFeatures = new PolygonFeature[map.Polygons?.Length ?? 0];
		if (map.Polygons != null)
			for (var i = 0; i < map.Polygons.Length; i++)
				PolyFeatures[i] = new PolygonFeature(map, LineFeatures, map.Polygons[i]);

		BasedMap = map;
	}

	public IEnumerable<PolygonFeature> FindPolygon(RectD region)
		=> PolyFeatures.Where(f => region.IntersectsWith(f.BoundingBox));
	public IEnumerable<PolygonFeature> FindPolygon(int code)
		=> PolyFeatures.Where(p => p.Code == code);
	public IEnumerable<PolygonFeature> FindPolygon(int code, int roundLevel)
		=> PolyFeatures.Where(p => (p.Code / roundLevel) == code);

	public void ClearCache()
	{
		foreach (var f in LineFeatures)
			f.ClearCache();
		foreach (var f in PolyFeatures)
			f.ClearCache();
	}

	/// <summary>
	/// 指定したズーム(±1)以外のキャッシュを解放する
	/// </summary>
	public void EvictCacheExcept(int[] activeZooms)
	{
		foreach (var f in LineFeatures)
			f.EvictCacheExcept(activeZooms);
		foreach (var f in PolyFeatures)
			f.EvictCacheExcept(activeZooms);
	}

	/// <summary>
	/// 指定したズームのキャッシュを保持すべきか(activeZoomsのいずれかの±1に入るか)
	/// </summary>
	internal static bool IsZoomRetained(int[] activeZooms, int zoom)
	{
		foreach (var active in activeZooms)
			if (Math.Abs(active - zoom) <= 1)
				return true;
		return false;
	}

	/// <summary>
	/// ズームをキーとするキャッシュ辞書を破棄する(辞書自体をロックとして使用する)
	/// </summary>
	internal static void ClearZoomCache<T>(Dictionary<int, T> cache)
	{
		lock (cache)
		{
			foreach (var value in cache.Values)
				if (value is IDisposable disposable)
					disposable.Dispose();
			cache.Clear();
		}
	}

	/// <summary>
	/// ズームをキーとするキャッシュ辞書から、保持対象外のズームの項目を破棄する(辞書自体をロックとして使用する)
	/// </summary>
	internal static void EvictZoomCacheExcept<T>(Dictionary<int, T> cache, int[] activeZooms)
	{
		lock (cache)
		{
			List<int>? removeZooms = null;
			foreach (var zoom in cache.Keys)
				if (!IsZoomRetained(activeZooms, zoom))
					(removeZooms ??= []).Add(zoom);
			if (removeZooms == null)
				return;
			foreach (var zoom in removeZooms)
			{
				if (cache[zoom] is IDisposable disposable)
					disposable.Dispose();
				cache.Remove(zoom);
			}
		}
	}
}
