using System.Collections.Generic;

namespace KyoshinEewViewer.Map.Layers;

/// <summary>
/// ズームレベルに応じて描画するレイヤーを変更する定義
/// </summary>
public record LandLayerSet(int MinZoom, LandLayerType LayerType)
{
	public static LandLayerSet[] DefaultLayerSets { get; } = [
		new(11, LandLayerType.MunicipalityEarthquakeTsunamiArea),
		new(0, LandLayerType.EarthquakeInformationSubdivisionArea),
	];
}

public static class LandLayerSetExtensions
{
	public static LandLayerType GetLayerType(this IEnumerable<LandLayerSet> layerSets, int zoom)
	{
		foreach (var layerSet in layerSets)
			if (zoom >= layerSet.MinZoom)
				return layerSet.LayerType;
		return LandLayerType.EarthquakeInformationPrefecture;
	}
}
