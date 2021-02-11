using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal sealed class LandLayer : MapLayerBase
	{
		public Point LeftTopLocation { get; set; }
		public Rect ViewAreaRect { get; set; }
		public LandLayerType PrimaryRenderLayer { get; set; } = LandLayerType.PrimarySubdivisionArea;

		private IDictionary<LandLayerType, FeatureCacheController> Controllers { get; set; }
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
					p.Key != LandLayerType.PrimarySubdivisionArea)
					return;
				controllers[p.Key] = new FeatureCacheController(p.Key, p.Value);
				//controllers[k].GenerateCache(Projection, minZoom, maxZoom);
			})).ToArray());
			Controllers = controllers;
		}

		#region ResourceCache
		private Pen CoastlineStroke { get; set; }
		private Pen PrefStroke { get; set; }
		private Pen AreaStroke { get; set; }

		private Brush LandFill { get; set; }
		private Brush OverSeasLandFill { get; set; }

		private bool InvalidateLandStroke { get; set; }
		private bool InvalidatePrefStroke { get; set; }
		private bool InvalidateAreaStroke { get; set; }

		public void RefleshResourceCache()
		{
			CoastlineStroke = new Pen((Brush)FindResource("LandStrokeColor"), (double)FindResource("LandStrokeThickness"));
			CoastlineStroke.Freeze();
			PrefStroke = new Pen((Brush)FindResource("PrefStrokeColor"), (double)FindResource("PrefStrokeThickness"));
			PrefStroke.Freeze();
			AreaStroke = new Pen((Brush)FindResource("AreaStrokeColor"), (double)FindResource("AreaStrokeThickness"));
			AreaStroke.Freeze();
			LandFill = (Brush)FindResource("LandColor");
			LandFill.Freeze();
			OverSeasLandFill = (Brush)FindResource("OverseasLandColor");
			OverSeasLandFill.Freeze();
			InvalidateLandStroke = (double)FindResource("LandStrokeThickness") <= 0;
			InvalidatePrefStroke = (double)FindResource("PrefStrokeThickness") <= 0;
			InvalidateAreaStroke = (double)FindResource("AreaStrokeThickness") <= 0;
		}
		#endregion

		protected override void OnRender(DrawingContext drawingContext)
		{
			// コントローラーの初期化ができていなければスキップ
			if (Controllers == null)
				return;

			// 使用するキャッシュのズーム
			var rZoom = (int)Math.Ceiling(Zoom);
			// 実際のズームに合わせるためのスケール
			var dZoom = Math.Pow(2, Zoom - rZoom);

			var leftTop = LeftTopLocation.CastLocation().ToPixel(Projection, rZoom);

			var transform = new TransformGroup
			{
				Children = new TransformCollection(new Transform[]
					{
						new TranslateTransform(-leftTop.X, -leftTop.Y),
						new ScaleTransform(dZoom, dZoom),
					})
			};
			transform.Freeze();

			// とりあえず海外の描画を行う
			RenderOverseas(drawingContext, transform, rZoom);

			var useLayerType = PrimaryRenderLayer;
			if (rZoom <= 5)
				useLayerType = LandLayerType.NationalAndRegionForecastArea;
			else if (rZoom <= 7)
				useLayerType = LandLayerType.PrefectureForecastArea;
			else if (rZoom <= 10)
				useLayerType = LandLayerType.PrimarySubdivisionArea;


			var landGeometry = new StreamGeometry() { Transform = transform };
			var prefGeometry = new StreamGeometry() { Transform = transform };
			var coastlineGeometry = new StreamGeometry() { Transform = transform };
			var areaGeometry = new StreamGeometry() { Transform = transform };
			using (var landStream = landGeometry.Open())
			using (var prefStream = prefGeometry.Open())
			using (var clineStream = coastlineGeometry.Open())
			using (var areaStream = areaGeometry.Open())
			{
				foreach (var f in Controllers[useLayerType].Find(ViewAreaRect))
				{
					switch (f.Type)
					{
						case FeatureType.Polygon:
							f.AddFigure(landStream, Projection, rZoom);
							break;
						case FeatureType.AdminBoundary:
							if (!InvalidatePrefStroke)
								f.AddFigure(prefStream, Projection, rZoom);
							break;
						case FeatureType.Coastline:
							if (!InvalidateLandStroke)
								f.AddFigure(clineStream, Projection, rZoom);
							break;
						case FeatureType.AreaBoundary:
							if (!InvalidateAreaStroke)
								f.AddFigure(areaStream, Projection, rZoom);
							break;
					}
				}
			}
			landGeometry.Freeze();
			coastlineGeometry.Freeze();
			prefGeometry.Freeze();
			areaGeometry.Freeze();

			// ズーム5未満では海外と同様に描画する
			drawingContext.DrawGeometry(LandFill, null, landGeometry);
			if (!InvalidateLandStroke && rZoom > 5)
				drawingContext.DrawGeometry(null, CoastlineStroke, coastlineGeometry);
			if (!InvalidatePrefStroke && rZoom > 5)
				drawingContext.DrawGeometry(null, PrefStroke, prefGeometry);
			if (!InvalidateAreaStroke && rZoom > 5)
				drawingContext.DrawGeometry(null, AreaStroke, areaGeometry);
		}
		/// <summary>
		/// 海外を描画する
		/// </summary>
		private void RenderOverseas(DrawingContext drawingContext, Transform transform, int rZoom)
		{
			if (!Controllers.ContainsKey(LandLayerType.WorldWithoutJapan))
				return;

			var overseasLandGeometry = new StreamGeometry() { Transform = transform };
			using (var overSeasLandStream = overseasLandGeometry.Open())
			{
				foreach (var f in Controllers[LandLayerType.WorldWithoutJapan].Find(ViewAreaRect))
				{
					if (f.Type != FeatureType.Polygon)
						continue;
					f.AddFigure(overSeasLandStream, Projection, rZoom);
				}
			}
			overseasLandGeometry.Freeze();

			drawingContext.DrawGeometry(OverSeasLandFill, null, overseasLandGeometry);
		}
	}
}
