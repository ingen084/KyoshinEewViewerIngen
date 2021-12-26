using Avalonia.Controls;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers;

public class OverlayLayer : MapLayer
{
	public IRenderObject[]? RenderObjects { get; set; }

	// TODO: なんかもうちょい細かく色指定できるようにしたほうがいい気もする
	private bool IsDarkTheme { get; set; }

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(Control targetControl)
	{
		bool FindBoolResource(string name)
			=> (bool)(targetControl.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
		IsDarkTheme = FindBoolResource("IsDarkTheme");
	}

	public override void Render(SKCanvas canvas, bool isAnimating)
	{
		if (RenderObjects == null)
			return;

		foreach (var o in RenderObjects)
			o.Render(canvas, PixelBound, Zoom, LeftTopPixel, isAnimating, IsDarkTheme);

		if (ViewAreaRect.Bottom > 180)
		{
			var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Zoom).X;
			var lt = LeftTopPixel;
			lt.X -= xLength;
			var pb = PixelBound;
			pb.X -= xLength;

			foreach (var o in RenderObjects)
				o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme);
		}
		else if (ViewAreaRect.Top < -180)
		{
			var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Zoom).X;
			var lt = LeftTopPixel;
			lt.X += xLength;
			var pb = PixelBound;
			pb.X += xLength;

			foreach (var o in RenderObjects)
				o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme);
		}
	}
}
