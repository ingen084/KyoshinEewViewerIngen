using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public abstract class RenderObject
	{
		public RenderObject()
		{
		}

		public abstract void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme);
	}
}