using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

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

		public Feature[] Find(Rect region)
		{
			var result = new List<Feature>();
			foreach (var f in Features)
			{
				if (region.IntersectsWith(f.BB))
					result.Add(f);
			}
			return result.ToArray();
		}
	}
}
