using KyoshinEewViewer.Map.Projections;
using SkiaSharp;

namespace KyoshinEewViewer.Map.Layers;

internal abstract class MapLayerBase
{
	protected MapLayerBase(MapProjection projection)
	{
		Projection = projection;
	}

	public MapProjection Projection { get; set; }
	public double Zoom { get; set; }
	public PointD LeftTopLocation { get; set; }
	public PointD LeftTopPixel { get; set; }
	public RectD PixelBound { get; set; }
	public RectD ViewAreaRect { get; set; }

	public abstract void Render(SKCanvas canvas, bool isAnimating);
}
