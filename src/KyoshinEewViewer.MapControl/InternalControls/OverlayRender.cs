using KyoshinEewViewer.MapControl.RenderObjects;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal sealed class OverlayRender : MapRenderBase
	{
		public Point LeftTopPixel { get; set; }
		public Rect PixelBound { get; set; }
		public RenderObject[] RenderObjects { get; set; }

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
