using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;

namespace KyoshinEewViewer.Map.Layers
{
	internal abstract class MapLayerBase
	{
		protected MapLayerBase(MapProjection projection)
		{
			Projection = projection;
		}

		public MapProjection Projection { get; set; }


		public abstract void OnRender(SKCanvas canvas, double zoom);
	}
}
