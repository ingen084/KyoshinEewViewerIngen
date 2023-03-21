using Avalonia.Controls;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss;

public class CurrentPositionLayer : MapLayer
{
	private Location? location;
	public Location? Location
	{
		get => location;
		set {
			location = value;
			RefleshRequest();
		}
	}

	private SKPaint CenterPaint { get; } = new SKPaint()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
		Color = SKColors.AliceBlue,
	};

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(Control targetControl) { }

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Location == null) return;

		var centerPosition = Location.ToPixel(param.Zoom) - param.LeftTopPixel;
		CenterPaint.Color = SKColors.AliceBlue;
		canvas.DrawCircle(centerPosition.AsSKPoint(), 7, CenterPaint);
		CenterPaint.Color = SKColors.RoyalBlue;
		canvas.DrawCircle(centerPosition.AsSKPoint(), 5, CenterPaint);
	}
}
