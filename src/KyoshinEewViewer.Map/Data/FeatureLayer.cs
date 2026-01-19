using KyoshinEewViewer.Core.Models;
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
		foreach (var f in PolyFeatures)
			f.ClearCache();
	}
}
