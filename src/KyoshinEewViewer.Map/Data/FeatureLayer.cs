using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KyoshinEewViewer.Map.Data;

public class FeatureLayer
{
	public TopologyMap BasedMap { get; }

	public PolylineFeature[] LineFeatures { get; }
	public PolygonFeature[] PolyFeatures { get; }

	public FeatureLayer(TopologyMap map)
	{
		LineFeatures = new PolylineFeature[map.Arcs?.Length ?? 0];
		if (map.Arcs != null)
			for (var i = 0; i < map.Arcs.Length; i++)
				LineFeatures[i] = new PolylineFeature(map, i);

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

	public void ClearCache()
	{
		foreach (var f in PolyFeatures)
			f.ClearCache();
	}
}
