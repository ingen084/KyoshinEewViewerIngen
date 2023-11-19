using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

	public static async Task<MapData> LoadDefaultMapAsync()
	{
		var mapData = new MapData();
		// 処理が重めなので雑に裏で
		await Task.Run(() =>
		{
			var collection = TopologyMap.LoadCollection(Properties.Resources.DefaultMap);
			// NOTE: とりあえず必要な分だけインスタンスを生成
			mapData.Layers[LandLayerType.WorldWithoutJapan] = new(collection[LandLayerType.WorldWithoutJapan]);
			mapData.Layers[LandLayerType.MunicipalityEarthquakeTsunamiArea] = new(collection[LandLayerType.MunicipalityEarthquakeTsunamiArea]);
			mapData.Layers[LandLayerType.EarthquakeInformationSubdivisionArea] = new(collection[LandLayerType.EarthquakeInformationSubdivisionArea]);
			mapData.Layers[LandLayerType.TsunamiForecastArea] = new(collection[LandLayerType.TsunamiForecastArea]);
		});
		return mapData;
	}
}
