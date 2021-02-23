using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Map.Projections;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Map.Layers
{
	internal class RealtimeOverlayLayer : MapLayerBase
	{
		private Timer Timer { get; }
		private TimeSpan RefreshInterval { get; } = TimeSpan.FromMilliseconds(100);
		private DateTime PrevTime { get; set; }

		public PointD LeftTopPixel { get; set; }
		public RectD PixelBound { get; set; }

		public RealtimeRenderObject[]? RealtimeRenderObjects { get; set; }

		// TODO: なんかもうちょい細かく色指定できるようにしたほうがいい気もする
		private bool IsDarkTheme { get; set; }

		public void RefleshResourceCache(Control control)
		{
			bool FindBoolResource(string name)
				=> (bool)(control.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
			IsDarkTheme = FindBoolResource("IsDarkTheme");
		}

		public RealtimeOverlayLayer(MapProjection proj, MapControl control) : base(proj)
		{
			Timer = new Timer(s =>
			{
				var now = DateTime.Now;
				var diff = now - PrevTime;
				PrevTime = now;

				if (RealtimeRenderObjects == null || !RealtimeRenderObjects.Any())
					return;

				foreach (var o in RealtimeRenderObjects)
				{
					o.TimeOffset += diff;
					o.OnTick();
				}

				Dispatcher.UIThread.InvokeAsync(control.InvalidateVisual).Wait();
				Timer?.Change(RefreshInterval, Timeout.InfiniteTimeSpan);
			}, null, RefreshInterval, Timeout.InfiniteTimeSpan);

			PrevTime = DateTime.Now;
		}
		~RealtimeOverlayLayer()
		{
			Timer.Change(Timeout.Infinite, Timeout.Infinite);
		}

		public override void OnRender(SKCanvas canvas, double zoom)
		{
			if (RealtimeRenderObjects == null)
				return;
			foreach (var o in RealtimeRenderObjects)
				o.Render(canvas, PixelBound, zoom, LeftTopPixel, IsDarkTheme, Projection);
		}
	}
}
