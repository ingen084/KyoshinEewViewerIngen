using KyoshinEewViewer.MapControl.Projections;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public interface IRenderObject
	{
		// TODO: まとめられないかな
		void Render(DrawingContext context, Rect viewRect, double zoom, Point leftTopPixel, bool isDarkTheme, MapProjection projection);
	}
}