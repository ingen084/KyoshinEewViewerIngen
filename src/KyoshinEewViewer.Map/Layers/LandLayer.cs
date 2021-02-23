﻿using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Map.Layers
{
	internal sealed class LandLayer : MapLayerBase
	{
		public LandLayer(MapProjection proj) : base(proj)
		{

		}

		public PointD LeftTopLocation { get; set; }
		public RectD ViewAreaRect { get; set; }
		/// <summary>
		/// 優先して描画するレイヤー
		/// </summary>
		//public LandLayerType PrimaryRenderLayer { get; set; } = LandLayerType.PrimarySubdivisionArea;

		private IDictionary<LandLayerType, FeatureCacheController> Controllers { get; set; } = new Dictionary<LandLayerType, FeatureCacheController>();
		public async Task SetupMapAsync(Dictionary<LandLayerType, TopologyMap> mapCollection, int minZoom, int maxZoom)
		{
			var controllers = new ConcurrentDictionary<LandLayerType, FeatureCacheController>();
			await Task.WhenAll(mapCollection.Select(p => Task.Run(() =>
			{
				// 市区町村のデータがでかすぎるのでいったん読み込まない
				// TODO: 制限を解除する
				if (p.Key != LandLayerType.WorldWithoutJapan &&
					p.Key != LandLayerType.NationalAndRegionForecastArea &&
					p.Key != LandLayerType.PrefectureForecastArea &&

					/*p.Key != LandLayerType.MunicipalityEarthquakeTsunamiArea &&*/

					p.Key != LandLayerType.PrimarySubdivisionArea)
					return;
				controllers[p.Key] = new FeatureCacheController(p.Key, p.Value);
				controllers[p.Key].GenerateCache(Projection, minZoom, maxZoom);
			})).ToArray());
			Controllers = controllers;
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

		private SKPaint LandFill { get; set; } = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = new SKColor(242, 239, 233),
		};
		private SKPaint OverSeasLandFill { get; set; } = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = new SKColor(169, 169, 169),
		};

		private bool InvalidateLandStroke => CoastlineStrokeWidth <= 0;
		private bool InvalidatePrefStroke => PrefStrokeWidth <= 0;
		private bool InvalidateAreaStroke => AreaStrokeWidth <= 0;

		public void RefleshResourceCache(Control control)
		{
			SKColor FindColorResource(string name)
				=> ((Color)(control.FindResource(name) ?? throw new Exception($"マップリソース {name} が見つかりませんでした"))).ToSKColor();
			float FindFloatResource(string name)
				=> (float)(control.FindResource(name) ?? throw new Exception($"マップリソース {name} が見つかりませんでした"));

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

			LandFill = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				Color = FindColorResource("LandColor"),
				IsAntialias = true,
			};

			OverSeasLandFill = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				Color = FindColorResource("OverseasLandColor"),
				IsAntialias = true,
			};
		}
		#endregion

		public override void OnRender(SKCanvas canvas, double zoom)
		{
			//e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			//e.Graphics.Clear(Color.Transparent);

			// コントローラーの初期化ができていなければスキップ
			if (Controllers == null)
				return;

			// 使用するキャッシュのズーム
			var baseZoom = (int)Math.Ceiling(zoom);
			// 実際のズームに合わせるためのスケール
			var scale = Math.Pow(2, zoom - baseZoom);

			var leftTop = LeftTopLocation.CastLocation().ToPixel(Projection, baseZoom);

			canvas.Scale((float)scale);
			canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

			// とりあえず海外の描画を行う
			RenderOverseas(canvas, baseZoom);

			var useLayerType = LandLayerType.NationalAndRegionForecastArea;
			if (baseZoom > 6)
				useLayerType = LandLayerType.PrefectureForecastArea;
			if (baseZoom > 8)
				useLayerType = LandLayerType.PrimarySubdivisionArea;
			//if (baseZoom > 10)
			//	useLayerType = LandLayerType.MunicipalityEarthquakeTsunamiArea;

			CoastlineStroke.StrokeWidth = (float)(CoastlineStrokeWidth / scale);
			PrefStroke.StrokeWidth = (float)(PrefStrokeWidth / scale);
			AreaStroke.StrokeWidth = (float)(AreaStrokeWidth / scale);

			if (!Controllers.TryGetValue(useLayerType, out var layer))
				return;
			foreach (var f in layer.Find(ViewAreaRect))
			{
				switch (f.Type)
				{
					case FeatureType.Polygon:
						f.Draw(canvas, Projection, baseZoom, LandFill);
						break;
					case FeatureType.AdminBoundary:
						if (!InvalidatePrefStroke && baseZoom > 5)
							f.Draw(canvas, Projection, baseZoom, PrefStroke);
						break;
					case FeatureType.Coastline:
						if (!InvalidateLandStroke && baseZoom > 5)
							f.Draw(canvas, Projection, baseZoom, CoastlineStroke);
						break;
					case FeatureType.AreaBoundary:
						if (!InvalidateAreaStroke && baseZoom > 5)
							f.Draw(canvas, Projection, baseZoom, AreaStroke);
						break;
				}
			}
		}
		/// <summary>
		/// 海外を描画する
		/// </summary>
		private void RenderOverseas(SKCanvas canvas, int baseZoom)
		{
			if (!Controllers.ContainsKey(LandLayerType.WorldWithoutJapan))
				return;

			foreach (var f in Controllers[LandLayerType.WorldWithoutJapan].Find(ViewAreaRect))
			{
				if (f.Type != FeatureType.Polygon)
					continue;
				f.Draw(canvas, Projection, baseZoom, OverSeasLandFill);
			}
		}
	}
}