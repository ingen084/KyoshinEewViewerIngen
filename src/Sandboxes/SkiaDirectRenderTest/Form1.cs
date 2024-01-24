using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using SkiaSharp;

namespace SkiaDirectRenderTest;

public partial class Form1 : Form
{
	public Form1()
	{
		InitializeComponent();
	}

	private LandLayer landLayer = new();
	private LandBorderLayer landBorderLayer = new();
	private MapLayerHost host = new();

	private Location CenterLocation = new(35.681236f, 139.767125f);
	private double Zoom = 8;

	private Point? beforePoint;

	override protected void OnLoad(EventArgs e)
	{
		base.OnLoad(e);

		var map = MapData.LoadDefaultMap();
		landLayer.Map = map;
		landBorderLayer.Map = map;
		host.Layers = [landLayer, landBorderLayer];
		host.WindowTheme = WindowTheme.Dark;
		host.RefreshRequested += skglControl1.Invalidate;

		skglControl1.MouseDown += (s, e) =>
		{
			if (e.Button != MouseButtons.Left)
				return;
			beforePoint = e.Location;
		};
		skglControl1.MouseMove += (s, e) =>
		{
			if (beforePoint is not { } bp)
				return;
			var newPosition = e.Location;
			var vector = new PointD(bp.X - newPosition.X, bp.Y - newPosition.Y);
			beforePoint = newPosition;

			CenterLocation = (CenterLocation.ToPixel(Zoom) + vector).ToLocation(Zoom);

			skglControl1.Invalidate();
		};
		skglControl1.MouseUp += (s, e) =>
		{
			if (e.Button != MouseButtons.Left)
				return;
			beforePoint = null;
		};
		skglControl1.MouseWheel += (s, e) =>
		{
			var mousePos = e.Location;
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(Zoom + (e.Delta / 120) * 0.25, 4, 18);
			if (Math.Abs(newZoom - Zoom) < .001)
				return;

			var newCenterPix = CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = new RectD(new PointD(Padding.Left, Padding.Top), new PointD(Math.Max(0, Width - Padding.Right), Math.Max(0, Height - Padding.Bottom)));
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			Zoom = newZoom;
			CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
			skglControl1.Invalidate();
		};
	}
	private Location GetLocation(Point p)
	{
		var paddedRect = new RectD(new PointD(Padding.Left, Padding.Top), new PointD(Math.Max(0, Width - Padding.Right), Math.Max(0, Height - Padding.Bottom)));

		var centerPix = CenterLocation.ToPixel(Zoom);
		var originPix = new PointD(centerPix.X + ((paddedRect.Width / 2) - p.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - p.Y) + paddedRect.Top);
		return originPix.ToLocation(Zoom);
	}

	private LayerRenderParameter CreateParameter()
	{
		// DP Cache
		var renderSize = Bounds;
		var paddedRect = new RectD(new PointD(Padding.Left, Padding.Top), new PointD(Math.Max(0, renderSize.Width - Padding.Right), Math.Max(0, renderSize.Height - Padding.Bottom)));

		var halfRenderSize = new PointD(paddedRect.Width / 2, paddedRect.Height / 2);
		// 左上/右下のピクセル座標
		var leftTop = CenterLocation.ToPixel(Zoom) - halfRenderSize - new PointD(Padding.Left, Padding.Top);
		var rightBottom = CenterLocation.ToPixel(Zoom) + halfRenderSize + new PointD(Padding.Right, Padding.Bottom);

		var leftTopLocation = leftTop.ToLocation(Zoom).CastPoint();

		return new()
		{
			LeftTopLocation = leftTopLocation,
			LeftTopPixel = leftTop,
			PixelBound = new RectD(leftTop, rightBottom),
			ViewAreaRect = new RectD(leftTopLocation, rightBottom.ToLocation(Zoom).CastPoint()),
			Padding = new(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom),
			Zoom = Zoom,
		};
	}

	private void skglControl1_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintGLSurfaceEventArgs e)
	{
		e.Surface.Canvas.Clear(SKColor.Parse(WindowTheme.Dark.MainBackgroundColor));

		var param = CreateParameter();
		host.Render(e.Surface.Canvas, param, false);
	}
}
