using Avalonia.Controls;
using KyoshinEewViewer.Map.Projections;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers
{
	internal class OverlayLayer : MapLayerBase
	{
		public OverlayLayer(MapProjection proj) : base(proj)
		{
		}

		public PointD LeftTopPixel { get; set; }
		public RectD PixelBound { get; set; }
		public RectD ViewAreaRect { get; set; }
		public IRenderObject[]? RenderObjects { get; set; }

		// TODO: なんかもうちょい細かく色指定できるようにしたほうがいい気もする
		private bool IsDarkTheme { get; set; }

		public void RefreshResourceCache(Control control)
		{
			bool FindBoolResource(string name)
				=> (bool)(control.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
			IsDarkTheme = FindBoolResource("IsDarkTheme");
		}

		public override void Render(SKCanvas canvas, bool isAnimating)
		{
			if (RenderObjects == null)
				return;

			foreach (var o in RenderObjects)
				o.Render(canvas, PixelBound, Zoom, LeftTopPixel, isAnimating, IsDarkTheme, Projection);

			if (ViewAreaRect.Bottom > 180)
			{
				var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Projection, Zoom).X;
				var lt = LeftTopPixel;
				lt.X -= xLength;
				var pb = PixelBound;
				pb.X -= xLength;

				foreach (var o in RenderObjects)
					o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme, Projection);
			}
			else if (ViewAreaRect.Top < -180)
			{
				var xLength = new KyoshinMonitorLib.Location(0, 180).ToPixel(Projection, Zoom).X;
				var lt = LeftTopPixel;
				lt.X += xLength;
				var pb = PixelBound;
				pb.X += xLength;

				foreach (var o in RenderObjects)
					o.Render(canvas, pb, Zoom, lt, isAnimating, IsDarkTheme, Projection);
			}
		}
	}
}
