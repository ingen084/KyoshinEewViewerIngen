using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal class OverlayLayer : MapLayerBase
	{
		public Point LeftTopPixel { get; set; }
		public Rect PixelBound { get; set; }
		public IRenderObject[] RenderObjects { get; set; }

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (RenderObjects == null)
				return;

			bool isDarkTheme = (bool)FindResource("IsDarkTheme");
			foreach (var o in RenderObjects)
				lock (o)
					o.Render(drawingContext, PixelBound, Zoom, LeftTopPixel, isDarkTheme);
		}
	}
}
