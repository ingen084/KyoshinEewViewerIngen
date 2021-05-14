using Avalonia.Controls;
using KyoshinEewViewer.Map.Projections;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Map.Layers
{
	internal class RealtimeOverlayLayer : MapLayerBase
	{
		private Stopwatch Stopwatch { get; }
		private TimeSpan PrevTime { get; set; }

		public PointD LeftTopPixel { get; set; }
		public RectD PixelBound { get; set; }

		public RealtimeRenderObject[]? RealtimeRenderObjects { get; set; }
		public RealtimeRenderObject[]? StandByRenderObjects { get; set; }

		// TODO: なんかもうちょい細かく色指定できるようにしたほうがいい気もする
		private bool IsDarkTheme { get; set; }

		public void RefleshResourceCache(Control control)
		{
			bool FindBoolResource(string name)
				=> (bool)(control.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
			IsDarkTheme = FindBoolResource("IsDarkTheme");
		}

		public RealtimeOverlayLayer(MapProjection proj) : base(proj)
		{
			Stopwatch = Stopwatch.StartNew();
			PrevTime = Stopwatch.Elapsed;
		}

		public override void Render(SKCanvas canvas)
		{
			var now = Stopwatch.Elapsed;
			var diff = now - PrevTime;
			PrevTime = now;
			if (RealtimeRenderObjects != null)
				foreach (var o in RealtimeRenderObjects)
				{
					o.TimeOffset += diff;
					o.OnTick();
					o.Render(canvas, PixelBound, Zoom, LeftTopPixel, IsDarkTheme, Projection);
				}
			if (StandByRenderObjects != null)
				foreach (var o in StandByRenderObjects)
				{
					o.TimeOffset += diff;
					o.OnTick();
				}
		}
	}
}
