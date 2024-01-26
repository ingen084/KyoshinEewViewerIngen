using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map.Data;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers;

public class LandBorderLayer : MapLayer
{
	private int LastZoomLevel { get; set; }
	private LandLayerType LastLayerType { get; set; }
	private void OnAsyncObjectGenerated(LandLayerType layerType, int zoom)
	{
		if (LastZoomLevel == zoom && LastLayerType == layerType)
			RefreshRequest();
	}
	private MapData? _map;
	public MapData? Map
	{
		get => _map;
		set {
			if (_map != null)
				_map.AsyncObjectGenerated -= OnAsyncObjectGenerated;
			_map = value;
			if (_map != null)
				_map.AsyncObjectGenerated += OnAsyncObjectGenerated;
			RefreshRequest();
		}
	}

	private LandLayerSet[] _layerSets = LandLayerSet.DefaultLayerSets;
	public LandLayerSet[] LayerSets
	{
		get => _layerSets;
		set {
			_layerSets = value;
			RefreshRequest();
		}
	}

	#region ResourceCache
	private SKPaint CoastlineStroke { get; set; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Green,
		StrokeWidth = 1,
		IsAntialias = true,
	};
	private float CoastlineStrokeWidth { get; set; } = 1;
	private SKPaint PrefStroke { get; set; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Green,
		StrokeWidth = .8f,
		IsAntialias = true,
	};
	private float PrefStrokeWidth { get; set; } = .8f;
	private SKPaint AreaStroke { get; set; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Green,
		StrokeWidth = .4f,
		IsAntialias = true,
	};
	private float AreaStrokeWidth { get; set; } = .4f;

	private bool InvalidateLandStroke => CoastlineStrokeWidth <= 0;
	private bool InvalidatePrefStroke => PrefStrokeWidth <= 0;
	private bool InvalidateAreaStroke => AreaStrokeWidth <= 0;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		CoastlineStroke = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColor.Parse(windowTheme.LandStrokeColor),
			StrokeWidth = windowTheme.LandStrokeThickness,
			IsAntialias = true,
		};
		CoastlineStrokeWidth = CoastlineStroke.StrokeWidth;

		PrefStroke = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColor.Parse(windowTheme.PrefStrokeColor),
			StrokeWidth = windowTheme.PrefStrokeThickness,
			IsAntialias = true,
		};
		PrefStrokeWidth = PrefStroke.StrokeWidth;

		AreaStroke = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColor.Parse(windowTheme.AreaStrokeColor),
			StrokeWidth = windowTheme.AreaStrokeThickness,
			IsAntialias = true,
		};
		AreaStrokeWidth = AreaStroke.StrokeWidth;
	}
	#endregion

	public override bool NeedPersistentUpdate => false;

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		// マップの初期化ができていなければスキップ
		if (Map == null)
			return;
		lock (Map)
		{
			canvas.Save();
			try
			{
				// 使用するキャッシュのズーム
				var baseZoom = (int)Math.Ceiling(param.Zoom);
				LastZoomLevel = baseZoom;
				// 実際のズームに合わせるためのスケール
				var scale = Math.Pow(2, param.Zoom - baseZoom);
				canvas.Scale((float)scale);
				// 画面座標への変換
				var leftTop = param.LeftTopLocation.CastLocation().ToPixel(baseZoom);
				canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

				// 使用するレイヤー決定
				var useLayerType = LayerSets.GetLayerType(baseZoom);
				LastLayerType = useLayerType;
				if (!Map.TryGetLayer(useLayerType, out var layer))
					return;

				// スケールに合わせてブラシのサイズ変更
				CoastlineStroke.StrokeWidth = (float)(CoastlineStrokeWidth / scale);
				PrefStroke.StrokeWidth = (float)(PrefStrokeWidth / scale);
				AreaStroke.StrokeWidth = (float)(AreaStrokeWidth / scale);

				RenderRect(param.ViewAreaRect);
				// 左右に途切れないように補完して描画させる
				if (param.ViewAreaRect.Bottom > 180)
				{
					canvas.Translate((float)new KyoshinMonitorLib.Location(0, 180).ToPixel(baseZoom).X, 0);

					var fixedRect = param.ViewAreaRect;
					fixedRect.Y -= 360;

					RenderRect(fixedRect);
				}
				else if (param.ViewAreaRect.Top < -180)
				{
					canvas.Translate(-(float)new KyoshinMonitorLib.Location(0, 180).ToPixel(baseZoom).X, 0);

					var fixedRect = param.ViewAreaRect;
					fixedRect.Y += 360;

					RenderRect(fixedRect);
				}

				void RenderRect(RectD subViewArea)
				{
					for (var i = 0; i < layer.LineFeatures.Length; i++)
					{
						var f = layer.LineFeatures[i];
						if (!subViewArea.IntersectsWith(f.BoundingBox))
							continue;
						switch (f.Type)
						{
							case PolylineType.AdminBoundary:
								if (!InvalidatePrefStroke && baseZoom > 4.5)
									f.Draw(canvas, baseZoom, PrefStroke);
								break;
							case PolylineType.Coastline:
								if (!InvalidateLandStroke && baseZoom > 4.5)
									f.Draw(canvas, baseZoom, CoastlineStroke);
								break;
							case PolylineType.AreaBoundary:
								if (!InvalidateAreaStroke && baseZoom > 4.5)
									f.Draw(canvas, baseZoom, AreaStroke);
								break;
						}
					}
				}
			}
			finally
			{
				canvas.Restore();
			}
		}
	}
}
