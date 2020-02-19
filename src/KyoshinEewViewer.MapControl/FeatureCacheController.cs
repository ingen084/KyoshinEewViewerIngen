using System.Collections.Generic;
using System.Windows;

namespace KyoshinEewViewer.MapControl
{
	public class FeatureCacheController
	{
		private List<Feature> Features { get; }
		public FeatureCacheController(TopologyMap map)
		{
			Features = new List<Feature>();
			foreach (var i in map.Polygons)
				Features.Add(new Feature(map, i));
			for (var i = 0; i < map.Arcs.Length; i++)
				Features.Add(new Feature(map, i));
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
