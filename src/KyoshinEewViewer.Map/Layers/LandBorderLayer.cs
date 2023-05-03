using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using KyoshinEewViewer.Map.Data;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers;

public class LandBorderLayer : MapLayer
{
	private MapData? _map;
	public MapData? Map
	{
		get => _map;
		set {
			_map = value;
			RefleshRequest();
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

	public override void RefreshResourceCache(Control targetControl)
	{
		SKColor FindColorResource(string name)
			=> ((Color)(targetControl.FindResource(name) ?? throw new Exception($"マップリソース {name} が見つかりませんでした"))).ToSKColor();
		float FindFloatResource(string name)
			=> (float)(targetControl.FindResource(name) ?? throw new Exception($"マップリソース {name} が見つかりませんでした"));

		CoastlineStroke = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = FindColorResource("LandStrokeColor"),
			StrokeWidth = FindFloatResource("LandStrokeThickness"),
			IsAntialias = true,
		};
		CoastlineStrokeWidth = CoastlineStroke.StrokeWidth;

		PrefStroke = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = FindColorResource("PrefStrokeColor"),
			StrokeWidth = FindFloatResource("PrefStrokeThickness"),
			IsAntialias = true,
		};
		PrefStrokeWidth = PrefStroke.StrokeWidth;

		AreaStroke = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = FindColorResource("AreaStrokeColor"),
			StrokeWidth = FindFloatResource("AreaStrokeThickness"),
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
		canvas.Save();
		lock (Map)
			try
			{
				// 使用するキャッシュのズーム
				var baseZoom = (int)Math.Ceiling(param.Zoom);
				// 実際のズームに合わせるためのスケール
				var scale = Math.Pow(2, param.Zoom - baseZoom);
				canvas.Scale((float)scale);
				// 画面座標への変換
				var leftTop = param.LeftTopLocation.CastLocation().ToPixel(baseZoom);
				canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

				// 使用するレイヤー決定
				var useLayerType = LandLayerType.EarthquakeInformationSubdivisionArea;
				if (baseZoom > 10)
					useLayerType = LandLayerType.MunicipalityEarthquakeTsunamiArea;

				// スケールに合わせてブラシのサイズ変更
				CoastlineStroke.StrokeWidth = (float)(CoastlineStrokeWidth / scale);
				PrefStroke.StrokeWidth = (float)(PrefStrokeWidth / scale);
				AreaStroke.StrokeWidth = (float)(AreaStrokeWidth / scale);

				if (!Map.TryGetLayer(useLayerType, out var layer))
					return;

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
						if (!subViewArea.IntersectsWith(f.Bb))
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
