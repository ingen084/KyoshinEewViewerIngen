using System.Collections.Generic;
using System.Diagnostics;

namespace KyoshinEewViewer.Map
{
	public class FeatureCacheController
	{
		public LandLayerType LayerType { get; }
		public TopologyMap BasedMap { get; }

		public Feature[] LineFeatures { get; }
		private Feature[] PolyFeatures { get; }
		public FeatureCacheController(LandLayerType layerType, TopologyMap map)
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

			LayerType = layerType;
			BasedMap = map;
		}

		public IEnumerable<Feature> FindPolygon(RectD region)
		{
			foreach (var f in PolyFeatures)
				if (region.IntersectsWith(f.BB))
					yield return f;
		}
		public IEnumerable<Feature> FindLine(RectD region)
		{
			foreach (var f in LineFeatures)
				if (region.IntersectsWith(f.BB))
					yield return f;
		}

		public void ClearCache()
		{
			foreach (var f in PolyFeatures)
				f.ClearCache();
		}
	}
}
