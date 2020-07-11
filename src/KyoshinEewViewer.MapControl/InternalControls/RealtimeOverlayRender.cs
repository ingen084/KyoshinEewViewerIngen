using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal class RealtimeOverlayRender : MapRenderBase
	{
		private DispatcherTimer Timer { get; } = new DispatcherTimer();
		private TimeSpan RefreshInterval { get; } = TimeSpan.FromMilliseconds(100);

		public Point LeftTopPixel { get; set; }
		public Rect PixelBound { get; set; }

		public RealtimeRenderObject[] RealtimeRenderObjects { get; set; }

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			Timer.Interval = RefreshInterval;
			Timer.Tick += (s, e) =>
			{
				if (RealtimeRenderObjects == null)
					return;

				foreach (var o in RealtimeRenderObjects)
				{
					o.OnTick();
					o.TimeOffset += RefreshInterval;
				}

				InvalidateVisual();
			};
			Timer.Start();
		}
		~RealtimeOverlayRender()
		{
			Timer.Stop();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (RealtimeRenderObjects == null)
				return;

			bool isDarkTheme = (bool)FindResource("IsDarkTheme");
			foreach (var o in RealtimeRenderObjects)
				lock (o)
					o.Render(drawingContext, PixelBound, Zoom, LeftTopPixel, isDarkTheme);
		}
	}
}
