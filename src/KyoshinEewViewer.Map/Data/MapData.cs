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

	private Dictionary<LandLayerType, FeatureLayer> Layers { get; } = [];
	protected Timer CacheClearTimer { get; }

	public MapData()
	{
		CacheClearTimer = new(s =>
		{
			lock (this)
				foreach (var l in Layers.Values)
					l.ClearCache();
		}, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
	}

	public bool TryGetLayer(LandLayerType layerType, out FeatureLayer layer)
		=> Layers.TryGetValue(layerType, out layer!);

	public static MapData LoadDefaultMap()
	{
		var mapData = new MapData();
		var sw = new Stopwatch();
		using var mapResource = new MemoryStream(Resources.world_mpk);
		var collection = TopologyMap.LoadCollection(mapResource);
		foreach (var (key, value) in collection)
		{
			value.AsyncObjectGenerated += z => mapData.AsyncObjectGenerated?.Invoke((LandLayerType)key, z);
			mapData.Layers[(LandLayerType)key] = new FeatureLayer(value);
		}
		Debug.WriteLine($"地図読込完了: {sw.ElapsedMilliseconds}ms");
		return mapData;
	}
}
