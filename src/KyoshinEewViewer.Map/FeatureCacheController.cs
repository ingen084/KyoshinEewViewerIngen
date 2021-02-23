using KyoshinEewViewer.Map.Projections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Map
{
	public class FeatureCacheController
	{
		public LandLayerType LayerType { get; }
		public TopologyMap BasedMap { get; }

		public Feature[] LineFeatures { get; }
		private Feature[] Features { get; }
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

			polyFeatures.AddRange(LineFeatures);
			Features = polyFeatures.ToArray();

			LayerType = layerType;
			BasedMap = map;
		}

		public void GenerateCache(MapProjection proj, int min, int max)
		{
			Debug.WriteLine("Generating Cache");

			Task.Run(() =>
			{
				for (var z = min; z <= max; z++)
				{
					var swc = Stopwatch.StartNew();
					foreach (var f in Features)
						f.GetOrCreatePointsCache(proj, z);
					Debug.WriteLine(z + " " + swc.ElapsedMilliseconds + "ms");
				}
			});
		}

		public IEnumerable<Feature> Find(RectD region)
		{
			foreach (var f in Features)
			{
				if (region.IntersectsWith(f.BB))
					yield return f;
			}
		}

		public void ClearCache()
		{
			foreach (var f in Features)
				f.ClearCache();
		}
	}
}
