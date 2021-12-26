using Avalonia.Controls;
using SkiaSharp;

namespace KyoshinEewViewer.Map.Layers;

public class LandBorderLayer : MapLayer
{
	private LandLayer ParentLandLayer { get; }

	public LandBorderLayer(LandLayer parentLandLayer)
	{
		ParentLandLayer = parentLandLayer;
	}

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(Control targetControl) { }

	public override void Render(SKCanvas canvas, bool isAnimating)
		=> ParentLandLayer.RenderLines(canvas);
}
