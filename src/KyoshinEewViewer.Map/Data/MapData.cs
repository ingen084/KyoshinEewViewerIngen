using KyoshinEewViewer.Map.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace KyoshinEewViewer.Map.Data;

public class MapData
{
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
		// 処理が重めなので雑に裏で
		var sw = new Stopwatch();
		using var mapResource = new MemoryStream(Resources.world_mpk);
		var collection = TopologyMap.LoadCollection(mapResource);
		foreach (var (key, value) in collection)
		{
			sw.Restart();
			mapData.Layers[(LandLayerType)key] = new(value);
		}
		Debug.WriteLine($"地図読込完了: {sw.ElapsedMilliseconds}ms");
		return mapData;
	}
}
