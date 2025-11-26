using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Ml.Layers;

/// <summary>
/// 機械学習用観測点レイヤー
/// </summary>
public class MlObservationPointLayer : MapLayer
{
	private SKPaint? _validPointPaint;
	private SKPaint? _invalidPointPaint;

	/// <summary>
	/// 観測点のリスト
	/// </summary>
	public IReadOnlyList<CommonObservationPoint> ObservationPoints { get; private set; } = [];

	public override bool NeedPersistentUpdate => false;

	/// <summary>
	/// 観測点を更新する
	/// </summary>
	/// <param name="points">新しい観測点のリスト</param>
	public void UpdateObservationPoints(IReadOnlyList<CommonObservationPoint> points)
	{
		ObservationPoints = points;
		RefreshRequest();
	}

	public override void RefreshResourceCache(WindowTheme theme)
	{
		// 強震モニタ座標が設定されている観測点
		_validPointPaint = new SKPaint
		{
			Color = new SKColor(255, 100, 100, 200),
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};

		// 強震モニタ座標が未設定の観測点
		_invalidPointPaint = new SKPaint
		{
			Color = new SKColor(128, 128, 128, 150),
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (ObservationPoints.Count == 0 || _validPointPaint == null || _invalidPointPaint == null)
			return;

		var zoom = param.Zoom;
		var radius = (float)Math.Max(2, Math.Min(6, zoom * 0.5));

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			foreach (var point in ObservationPoints)
			{
				if (point.Location == null)
					continue;

				var pixelPoint = point.Location.ToPixel(zoom);

				// 画面外の場合はスキップ
				if (!param.PixelBound.Contains(pixelPoint))
					continue;

				// 強震モニタ座標の有無で色を変える
				var paint = point.Point != null ? _validPointPaint : _invalidPointPaint;
				canvas.DrawCircle((float)pixelPoint.X, (float)pixelPoint.Y, radius, paint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
