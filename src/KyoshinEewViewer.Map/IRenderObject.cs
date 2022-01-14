using SkiaSharp;

namespace KyoshinEewViewer.Map;

public interface IRenderObject
{
	void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme);
}
