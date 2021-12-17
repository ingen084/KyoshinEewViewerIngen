using System.Collections.Generic;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Map.Data;

public class MapData
{
	private Dictionary<LandLayerType, FeatureLayer> Layers { get; } = new();

	public Task LoadAsync(Dictionary<LandLayerType, TopologyMap> mapCollection)
		=> Task.Run(() =>
			{
				Layers[LandLayerType.WorldWithoutJapan] = new(mapCollection[LandLayerType.WorldWithoutJapan]);
				Layers[LandLayerType.MunicipalityEarthquakeTsunamiArea] = new(mapCollection[LandLayerType.MunicipalityEarthquakeTsunamiArea]);
				Layers[LandLayerType.EarthquakeInformationSubdivisionArea] = new(mapCollection[LandLayerType.EarthquakeInformationSubdivisionArea]);
			});

	public bool TryGetLayer(LandLayerType layerType, out FeatureLayer layer)
#pragma warning disable CS8601 // Null 参照代入の可能性があります。
		=> Layers.TryGetValue(layerType, out layer);
#pragma warning restore CS8601 // Null 参照代入の可能性があります。
}
