using System.Collections.Generic;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Map.Data;

public class MapData
{
	private Dictionary<LandLayerType, FeatureLayer> Layers { get; } = new();

	public bool TryGetLayer(LandLayerType layerType, out FeatureLayer layer)
#pragma warning disable CS8601 // Null 参照代入の可能性があります。
		=> Layers.TryGetValue(layerType, out layer);
#pragma warning restore CS8601 // Null 参照代入の可能性があります。

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
		});
		return mapData;
	}
}
