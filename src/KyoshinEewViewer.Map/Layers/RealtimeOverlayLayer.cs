using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Map.Projections;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace KyoshinEewViewer.Map.Layers
{
	internal class RealtimeOverlayLayer : MapLayerBase
	{
		private Timer Timer { get; }
		private TimeSpan RefreshInterval { get; } = TimeSpan.FromMilliseconds(20);
		private Stopwatch Stopwatch { get; }
		private TimeSpan PrevTime { get; set; }

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
				//if (RealtimeRenderObjects != null && RealtimeRenderObjects.Any())
				//	Dispatcher.UIThread.InvokeAsync(control.InvalidateVisual).Wait();
				Timer?.Change(RefreshInterval, Timeout.InfiniteTimeSpan);
			}, null, RefreshInterval, Timeout.InfiniteTimeSpan);

			Stopwatch = Stopwatch.StartNew();
			PrevTime = Stopwatch.Elapsed;
		}
		~RealtimeOverlayLayer()
		{
			Timer.Change(Timeout.Infinite, Timeout.Infinite);
		}

		public override void Render(SKCanvas canvas)
		{
			var now = Stopwatch.Elapsed;
			var diff = now - PrevTime;
			PrevTime = now;
			if (RealtimeRenderObjects == null)
				return;
			foreach (var o in RealtimeRenderObjects)
			{
				o.TimeOffset += diff;
				o.OnTick();
				o.Render(canvas, PixelBound, Zoom, LeftTopPixel, IsDarkTheme, Projection);
			}
		}
	}
}
