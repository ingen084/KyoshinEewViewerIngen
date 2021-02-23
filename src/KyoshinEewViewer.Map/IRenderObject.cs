using KyoshinEewViewer.Map.Projections;
using SkiaSharp;

namespace KyoshinEewViewer.Map
{
	public interface IRenderObject
	{
		void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isDarkTheme, MapProjection projection);
	}
}
