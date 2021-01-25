using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal class RealtimeOverlayLayer : MapLayerBase
	{
		private DispatcherTimer Timer { get; } = new DispatcherTimer(DispatcherPriority.ApplicationIdle);
		private TimeSpan RefreshInterval { get; } = TimeSpan.FromMilliseconds(20);
		private DateTime PrevTime { get; set; }

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

				var now = DateTime.Now;
				var diff = now - PrevTime;
				PrevTime = now;

				foreach (var o in RealtimeRenderObjects)
				{
					o.TimeOffset += diff;
					o.OnTick();
				}

				InvalidateVisual();
			};
			PrevTime = DateTime.Now;
			Timer.Start();
		}
		~RealtimeOverlayLayer()
		{
			Timer.Stop();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (RealtimeRenderObjects == null)
				return;

			bool isDarkTheme = (bool)FindResource("IsDarkTheme");
			foreach (var o in RealtimeRenderObjects)
				o.Render(drawingContext, PixelBound, Zoom, LeftTopPixel, isDarkTheme, Projection);
		}
	}
}
