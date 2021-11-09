using Avalonia.Controls;
using KyoshinEewViewer.Map.Projections;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Map.Layers;

internal class RealtimeOverlayLayer : MapLayerBase
{
	private Stopwatch Stopwatch { get; }
	private TimeSpan PrevTime { get; set; }

	public RealtimeRenderObject[]? RealtimeRenderObjects { get; set; }
	public RealtimeRenderObject[]? StandByRenderObjects { get; set; }

	// TODO: なんかもうちょい細かく色指定できるようにしたほうがいい気もする
	private bool IsDarkTheme { get; set; }

	public void RefreshResourceCache(Control control)
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

	private readonly object _lockObject = new();

	public override void Render(SKCanvas canvas, bool isAnimating)
	{
		lock (_lockObject)
		{
			var now = Stopwatch.Elapsed;
			var diff = now - PrevTime;
			PrevTime = now;
			if (RealtimeRenderObjects != null)
			{
				foreach (var o in RealtimeRenderObjects)
				{
					o.TimeOffset += diff;
					o.OnTick();
					o.Render(canvas, PixelBound, Zoom, LeftTopPixel, isAnimating, IsDarkTheme, Projection);
				}

				if (ViewAreaRect.Bottom > 180)
				{
					var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Projection, Zoom).X;
					var lt = LeftTopPixel;
					lt.X -= xLength;
					var pb = PixelBound;
					pb.X -= xLength;

					foreach (var o in RealtimeRenderObjects)
						o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme, Projection);
				}
				else if (ViewAreaRect.Top < -180)
				{
					var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Projection, Zoom).X;
					var lt = LeftTopPixel;
					lt.X += xLength;
					var pb = PixelBound;
					pb.X += xLength;

					foreach (var o in RealtimeRenderObjects)
						o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme, Projection);
				}

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
