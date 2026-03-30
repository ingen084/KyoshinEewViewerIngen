using KyoshinEewViewer.Map.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace KyoshinEewViewer.Map.Data;

public class MapData
{
	public event Action<LandLayerType, int>? AsyncObjectGenerated;

	private Dictionary<LandLayerType, Lazy<FeatureLayer>> LazyLayers { get; } = [];
	protected Timer CacheClearTimer { get; }

	/// <summary>
	/// 読み込み済みのデフォルトマップデータ（キャッシュ）
	/// </summary>
	public static MapData? CachedMap { get; private set; }

	public MapData()
	{
		CacheClearTimer = new(s =>
		{
			lock (this)
				foreach (var l in LazyLayers.Values)
					if (l.IsValueCreated)
						l.Value.ClearCache();
		}, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
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
			value.AsyncObjectGenerated += z => mapData.AsyncObjectGenerated?.Invoke(layerType, z);
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
