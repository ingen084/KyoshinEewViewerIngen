using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.RenderObjects
{
	public class EewPSWaveRenderObject : RealtimeRenderObject
	{
		private static Pen PWaveStrokePen;
		private static Pen SWaveStrokePen;
		private static Brush SWaveFillBrush;

		private TrTimeTableService TrTimeTableService { get; }
		private bool NeedUpdateGeometry { get; set; }

		private Eew eew;

		public Eew Eew
		{
			get => eew;
			set
			{
				eew = value;
				BaseTime = eew.UpdatedTime;
				NeedUpdateGeometry = true;
			}
		}

		/// <summary>
		/// 中心座標からのオフセット(メートル)
		/// </summary>
		public Point Offset { get; set; }

		private double? PWaveDistance { get; set; }
		private Geometry PWaveGeometryCache { get; set; }
		private double? SWaveDistance { get; set; }
		private Geometry SWaveGeometryCache { get; set; }
		private double CachedZoom { get; set; }

		private void MakeWaveGeometry(double zoom)
		{
			(var p, var s) = TrTimeTableService.CalcDistance(eew.OccurrenceTime, BaseTime + TimeOffset, eew.Depth);

			// 変化がなさそうであれば帰る
			if (CachedZoom == zoom && !NeedUpdateGeometry &&
				p == PWaveDistance && s == SWaveDistance)
				return;

			CachedZoom = zoom;

			if (p is double pDistance)
				PWaveGeometryCache = GeometryGenerator.MakeCircleGeometry(eew.Location, pDistance, zoom);
			else
				PWaveGeometryCache = null;

			if (s is double sDistance)
				SWaveGeometryCache = GeometryGenerator.MakeCircleGeometry(eew.Location, sDistance, zoom);
			else
				SWaveGeometryCache = null;
		}

		public EewPSWaveRenderObject(TrTimeTableService trTimeTableService, DateTime currentTime, Eew eew)
		{
			InitalizeBrushes();

			TrTimeTableService = trTimeTableService ?? throw new ArgumentNullException(nameof(trTimeTableService));
			BaseTime = currentTime;
			Eew = eew;

			NeedUpdateGeometry = true;
		}
		private void InitalizeBrushes()
		{
			if (PWaveStrokePen != null)
				return;
			PWaveStrokePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 160, 255)), 1);
			PWaveStrokePen.Freeze();
			SWaveStrokePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 80, 120)), 1);
			SWaveStrokePen.Freeze();
			SWaveFillBrush = new RadialGradientBrush(
				new GradientStopCollection(new[]
				{
					new GradientStop(Color.FromArgb(15, 255, 80, 120), .6),
					new GradientStop(Color.FromArgb(80, 255, 80, 120), 1)
				})
			);
			SWaveFillBrush.Freeze();
		}

		public override void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme)
		{
			MakeWaveGeometry(zoom);

			RenderCircleGeometry(PWaveGeometryCache, context, leftTopPixel, null, PWaveStrokePen);
			RenderCircleGeometry(SWaveGeometryCache, context, leftTopPixel, SWaveFillBrush, SWaveStrokePen);
		}
		private void RenderCircleGeometry(Geometry geometry, DrawingContext context, Point leftTopPixel, Brush brush, Pen pen)
		{
			if (geometry == null)
				return;
			if (!(geometry.Transform is TranslateTransform tt))
				geometry.Transform = new TranslateTransform(-leftTopPixel.X, -leftTopPixel.Y);
			else
			{
				tt.X = -leftTopPixel.X;
				tt.Y = -leftTopPixel.Y;
			}
			context.DrawGeometry(brush, pen, geometry);
		}

		protected override void OnTick()
		{
			NeedUpdateGeometry = true;
		}
	}
}