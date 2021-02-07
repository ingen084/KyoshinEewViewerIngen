using KyoshinEewViewer.MapControl.Projections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace KyoshinEewViewer.MapControl
{
	public class FeatureCacheController
	{
		public Feature[] LineFeatures { get; }
		private Feature[] Features { get; }
		public FeatureCacheController(TopologyMap map)
		{
			var polyFeatures = new List<Feature>();
			var lineFeatures = new List<Feature>();

			Debug.WriteLine("Generating FeatureObject");
			var sw = Stopwatch.StartNew();

			for (var i = 0; i < map.Arcs.Length; i++)
				lineFeatures.Add(new Feature(map, i));

			Debug.WriteLine("LineFeature: " + sw.ElapsedMilliseconds + "ms");

			LineFeatures = lineFeatures.ToArray();

			sw.Restart();

			foreach (var i in map.Polygons)
				polyFeatures.Add(new Feature(map, LineFeatures, i));

			Debug.WriteLine("PolyFeature: " + sw.ElapsedMilliseconds + "ms");

			polyFeatures.AddRange(LineFeatures);
			Features = polyFeatures.ToArray();
		}

		public void GenerateCache(MapProjection proj, int min, int max)
		{
			Debug.WriteLine("Generating Cache");

			for (var z = min; z <= max; z++)
			{
				var tz = z;
				Task.Run(() =>
				{
					var swc = Stopwatch.StartNew();
					foreach (var f in Features)
						f.CreatePointsCache(proj, tz);
					Debug.WriteLine(tz + " " + swc.ElapsedMilliseconds + "ms");
				});
			}
		}

		public IEnumerable<Feature> Find(Rect region)
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
