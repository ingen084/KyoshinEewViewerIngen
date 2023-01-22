using System;
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
		var polyFeatures = new List<PolygonFeature>();
		var lineFeatures = new List<PolylineFeature>();

		Debug.WriteLine("Generating FeatureObject");
		var sw = Stopwatch.StartNew();

		if (map.Arcs != null)
			for (var i = 0; i < map.Arcs.Length; i++)
				lineFeatures.Add(new PolylineFeature(map, i));

		Debug.WriteLine("LineFeature: " + sw.ElapsedMilliseconds + "ms");

		LineFeatures = lineFeatures.ToArray();

		sw.Restart();

		if (map.Polygons != null)
			foreach (var i in map.Polygons)
				polyFeatures.Add(new PolygonFeature(map, LineFeatures, i));

		Debug.WriteLine("PolyFeature: " + sw.ElapsedMilliseconds + "ms");

		PolyFeatures = polyFeatures.ToArray();

		BasedMap = map;
	}

	public IEnumerable<PolygonFeature> FindPolygon(RectD region)
		=> PolyFeatures.Where(f => region.IntersectsWith(f.BB));
	public PolygonFeature? FindPolygon(int code)
		=> PolyFeatures.FirstOrDefault(p => p.Code == code);

	public void ClearCache()
	{
		foreach (var f in PolyFeatures)
			f.ClearCache();
	}
}
