using KyoshinEewViewer.Map.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace KyoshinEewViewer.Map.Data;

public class MapData
{
	public event Action<LandLayerType, int>? AsyncObjectGenerated;
	internal void OnAsyncObjectGenerated(LandLayerType layerType, int zoom)
		=> AsyncObjectGenerated?.Invoke(layerType, zoom);

	private Dictionary<LandLayerType, Lazy<FeatureLayer>> LazyLayers { get; } = [];
	protected Timer CacheClearTimer { get; }

	/// <summary>
	/// 描画に使用されたズームレベルと最終使用時刻(Environment.TickCount64)
	/// </summary>
	private ConcurrentDictionary<int, long> RecentUsedZooms { get; } = [];
	/// <summary>
	/// この期間内に使用されたズーム(±1)のキャッシュを保持する
	/// </summary>
	private static readonly TimeSpan ZoomRetentionPeriod = TimeSpan.FromMinutes(3);

	/// <summary>
	/// 読み込み済みのデフォルトマップデータ（キャッシュ）
	/// </summary>
	public static MapData? CachedMap { get; private set; }

	public MapData()
	{
		CacheClearTimer = new(s =>
		{
			// 期限切れのズーム記録を破棄し、残ったズーム(±1)以外のキャッシュを解放する
			var threshold = Environment.TickCount64 - (long)ZoomRetentionPeriod.TotalMilliseconds;
			foreach (var (zoom, lastUsed) in RecentUsedZooms)
				if (lastUsed < threshold)
					RecentUsedZooms.TryRemove(zoom, out _);
			var activeZooms = RecentUsedZooms.Keys.ToArray();
			lock (this)
				foreach (var l in LazyLayers.Values)
					if (l.IsValueCreated)
					{
						// 一定期間描画されていない場合(ウィンドウ最小化中など)は全て解放する
						if (activeZooms.Length == 0)
							l.Value.ClearCache();
						else
							l.Value.EvictCacheExcept(activeZooms);
					}
		}, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
	}

	private int _lastReportedZoom = -1;
	private long _lastReportedTime;
	/// <summary>
	/// キャッシュ利用中のズームレベルを記録する(キャッシュ解放の判断に使用)
	/// </summary>
	private void ReportZoomUsed(int zoom)
	{
		// 描画中は同じズームが大量に通知されるため間引く
		var now = Environment.TickCount64;
		if (zoom == _lastReportedZoom && now - _lastReportedTime < 1000)
			return;
		_lastReportedZoom = zoom;
		_lastReportedTime = now;
		RecentUsedZooms[zoom] = now;
	}

	public bool TryGetLayer(LandLayerType layerType, out FeatureLayer layer)
	{
		if (LazyLayers.TryGetValue(layerType, out var lazyLayer))
		{
			layer = lazyLayer.Value;
			return true;
		}
		layer = null!;
		return false;
	}

	public static MapData LoadDefaultMap()
	{
		var mapData = new MapData();
		var sw = new Stopwatch();
		using var mapResource = new MemoryStream(Resources.world_mpk);
		var collection = TopologyMap.LoadCollection(mapResource);
		foreach (var (key, value) in collection)
		{
			var layerType = (LandLayerType)key;
			value.AsyncObjectGenerated += z => mapData.OnAsyncObjectGenerated(layerType, z);
			value.ZoomUsed += mapData.ReportZoomUsed;
			mapData.LazyLayers[layerType] = new Lazy<FeatureLayer>(
				() => new FeatureLayer(value),
				LazyThreadSafetyMode.ExecutionAndPublication);
		}
		sw.Stop();
		Debug.WriteLine($"地図読込完了: {sw.ElapsedMilliseconds}ms");
		CachedMap = mapData;
		return mapData;
	}
}
