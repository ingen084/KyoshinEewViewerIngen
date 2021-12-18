using Avalonia.Controls;
using KyoshinEewViewer.Map.Projections;
using SkiaSharp;

namespace KyoshinEewViewer.Map.Layers;

internal class LandBorderLayer : MapLayerBase
{
	private LandLayer ParentLandLayer { get; }

	public LandBorderLayer(LandLayer parentLandLayer, MapProjection projection) : base(projection)
	{
		ParentLandLayer = parentLandLayer;
	}

	public override void RefreshResourceCache(Control targetControl) { }

	public override void Render(SKCanvas canvas, bool isAnimating)
		=> ParentLandLayer.RenderLines(canvas);
}
