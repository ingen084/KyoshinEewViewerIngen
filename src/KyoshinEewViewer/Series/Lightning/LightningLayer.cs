using Avalonia.Controls;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Lightning;

public class LightningLayer(TimerService timer) : MapLayer
{
	// 音の秒速
	private const double MachPerSecond = 1225000.0 / 60 / 60;
	private static readonly SKPaint BorderPen = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
		Color = new SKColor(255, 255, 255, 120),
		IsAntialias = true,
	};
	private static readonly SKPaint CenterPen = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
		Color = new SKColor(255, 0, 0, 255),
	};
	private static PointD _markerSize = new(5, 5);

	private List<(DateTime occuraceTime, Location location, DateTime receivedTime)> Lightnings { get; } = [];

	// 最後描画した際に画面内に描画対象が存在したか
	private bool IsLatestVisible { get; set; }
	public override bool NeedPersistentUpdate => IsLatestVisible;

	private TimerService Timer { get; } = timer;

	public override void RefreshResourceCache(Control targetControl) { }
	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		var isLatestVisible = false;

		canvas.Save();
		canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);
		try
		{
			foreach (var lightning in Lightnings.ToArray())
			{
				var secs = (Timer.CurrentTime - lightning.occuraceTime).TotalSeconds;
				// 20秒経過で削除
				if (secs >= 20)
					Lightnings.Remove(lightning);

				var pointCenter = lightning.location.ToPixel(param.Zoom);
				if (!param.PixelBound.IntersectsWith(new RectD(pointCenter - _markerSize, pointCenter + _markerSize)))
					continue;

				var basePoint = lightning.location.ToPixel(param.Zoom);
				canvas.DrawLine((basePoint - new PointD(5, 5)).AsSkPoint(), (basePoint + new PointD(5, 5)).AsSkPoint(), CenterPen);
				canvas.DrawLine((basePoint - new PointD(-5, 5)).AsSkPoint(), (basePoint + new PointD(-5, 5)).AsSkPoint(), CenterPen);

				var arrSecs = (Timer.CurrentTime - lightning.receivedTime).TotalSeconds;
				if (arrSecs <= .5)
				{
					canvas.DrawCircle(basePoint.AsSkPoint(), (float)(1 - arrSecs) * 20, CenterPen);
					isLatestVisible = true;
				}

				var distance = secs * MachPerSecond;
				if (param.Zoom <= 6 || distance <= 0)
					continue;

				isLatestVisible = true;

				var cache = PathGenerator.MakeCirclePath(lightning.location, distance, param.Zoom, (int)(param.Zoom - 5) * 30);

				// 発生からの秒数に合わせて縁の太さを変える
				BorderPen.StrokeWidth = (float)Math.Max(1, secs / 7);

				canvas.DrawPath(cache, BorderPen);
			}
		}
		finally
		{
			canvas.Restore();
		}

		IsLatestVisible = isLatestVisible;
	}

	public void Appear(DateTime occuraceTime, Location location)
	{
		Lightnings.Add((occuraceTime, location, Timer.CurrentTime));
		RefleshRequest();
	}
}
