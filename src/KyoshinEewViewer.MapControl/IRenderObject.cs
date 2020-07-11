using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public interface IRenderObject
	{
		void Render(DrawingContext context, Rect viewRect, double zoom, Point leftTopPixel, bool isDarkTheme);
	}
}