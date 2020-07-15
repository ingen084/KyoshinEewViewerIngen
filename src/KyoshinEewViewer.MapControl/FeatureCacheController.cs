using System.Collections.Generic;
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

			for (var i = 0; i < map.Arcs.Length; i++)
				lineFeatures.Add(new Feature(map, i));

			LineFeatures = lineFeatures.ToArray();

			foreach (var i in map.Polygons)
				polyFeatures.Add(new Feature(map, LineFeatures, i));

			polyFeatures.AddRange(LineFeatures);
			Features = polyFeatures.ToArray();
		}

		public IEnumerable<Feature> Find(Rect region)
		{
			foreach (var f in Features)
			{
				if (region.IntersectsWith(f.BB))
					yield return f;
			}
		}
	}
}
