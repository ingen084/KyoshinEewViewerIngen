using Avalonia.Controls;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Map.Layers;

public class RealtimeOverlayLayer : MapLayer
{
	private Stopwatch Stopwatch { get; }
	private TimeSpan PrevTime { get; set; }

	public RealtimeRenderObject[]? RealtimeRenderObjects { get; set; }

	private bool IsDarkTheme { get; set; }

	public RealtimeOverlayLayer()
	{
		Stopwatch = Stopwatch.StartNew();
		PrevTime = Stopwatch.Elapsed;
	}

	public override bool NeedPersistentUpdate => (RealtimeRenderObjects?.Length ?? 0) >= 0;

	public override void RefreshResourceCache(Control targetControl)
	{
		bool FindBoolResource(string name)
			=> (bool)(targetControl.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
		IsDarkTheme = FindBoolResource("IsDarkTheme");
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
					o.Render(canvas, PixelBound, Zoom, LeftTopPixel, isAnimating, IsDarkTheme);
				}

				if (ViewAreaRect.Bottom > 180)
				{
					var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Zoom).X;
					var lt = LeftTopPixel;
					lt.X -= xLength;
					var pb = PixelBound;
					pb.X -= xLength;

					foreach (var o in RealtimeRenderObjects)
						o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme);
				}
				else if (ViewAreaRect.Top < -180)
				{
					var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Zoom).X;
					var lt = LeftTopPixel;
					lt.X += xLength;
					var pb = PixelBound;
					pb.X += xLength;

					foreach (var o in RealtimeRenderObjects)
						o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme);
				}
			}
		}
	}
}
