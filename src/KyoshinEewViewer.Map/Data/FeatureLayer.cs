using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KyoshinEewViewer.Map.Data;

public class FeatureLayer
{
	public TopologyMap BasedMap { get; }

	public Feature[] LineFeatures { get; }
	private Feature[] PolyFeatures { get; }
	public FeatureLayer(TopologyMap map)
	{
		var polyFeatures = new List<Feature>();
		var lineFeatures = new List<Feature>();

		Debug.WriteLine("Generating FeatureObject");
		var sw = Stopwatch.StartNew();

		if (map.Arcs != null)
			for (var i = 0; i < map.Arcs.Length; i++)
				lineFeatures.Add(new Feature(map, i));

		Debug.WriteLine("LineFeature: " + sw.ElapsedMilliseconds + "ms");

		LineFeatures = lineFeatures.ToArray();

		sw.Restart();

		if (map.Polygons != null)
			foreach (var i in map.Polygons)
				polyFeatures.Add(new Feature(map, LineFeatures, i));

		Debug.WriteLine("PolyFeature: " + sw.ElapsedMilliseconds + "ms");

		//polyFeatures.AddRange(LineFeatures);
		PolyFeatures = polyFeatures.ToArray();

		BasedMap = map;
	}

	public IEnumerable<Feature> FindPolygon(RectD region)
		=> PolyFeatures.Where(f => region.IntersectsWith(f.BB));
	public IEnumerable<Feature> FindLine(RectD region)
		=> LineFeatures.Where(f => region.IntersectsWith(f.BB));

	public void ClearCache()
	{
		foreach (var f in PolyFeatures)
			f.ClearCache();
	}
}
