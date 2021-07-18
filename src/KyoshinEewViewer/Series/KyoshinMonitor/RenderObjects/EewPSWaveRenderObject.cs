using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor.RenderObjects
{
	public class EewPSWaveRenderObject : RealtimeRenderObject
	{
		private static SKPaint PWavePaint;
		private static SKPaint SWavePaint;

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
		public PointD Offset { get; set; }

		private double? PWaveDistance { get; set; }
		private SKPath? PWaveGeometryCache { get; set; }
		private double? SWaveDistance { get; set; }
		private SKPath? SWaveGeometryCache { get; set; }
		private double CachedZoom { get; set; }

		private void MakeWaveGeometry(MapProjection projection, double zoom)
		{
			(var p, var s) = TravelTimeTableService.CalcDistance(eew.OccurrenceTime, BaseTime + TimeOffset, eew.Depth);

			// 変化がなさそうであれば帰る
			if (CachedZoom == zoom && !NeedUpdateGeometry &&
				p == PWaveDistance && s == SWaveDistance)
				return;

			CachedZoom = zoom;

			if (p is double pDistance)
				PWaveGeometryCache = PathGenerator.MakeCirclePath(projection, eew.Location, pDistance * 1000, zoom);
			else
				PWaveGeometryCache = null;

			if (s is double sDistance)
				SWaveGeometryCache = PathGenerator.MakeCirclePath(projection, eew.Location, sDistance * 1000, zoom);
			else
				SWaveGeometryCache = null;
		}


		static EewPSWaveRenderObject()
		{
			PWavePaint = new SKPaint
			{
				IsAntialias = true,
				Style = SKPaintStyle.Stroke,
				StrokeWidth = 1,
				Color = new SKColor(0, 160, 255, 200),
			};
			SWavePaint = new SKPaint
			{
				IsAntialias = true,
				Style = SKPaintStyle.Stroke,
				StrokeWidth = 1,
				Color = new SKColor(255, 80, 120),
			};
		}
		public EewPSWaveRenderObject(DateTime currentTime, Eew eew)
		{
			BaseTime = currentTime;
			this.eew = eew;

			NeedUpdateGeometry = true;
		}

		public override void Render(SKCanvas canvas, RectD bound, double zoom, PointD leftTopPixel, bool isAnimating, bool isDark, MapProjection projection)
		{
			MakeWaveGeometry(projection, zoom);

			canvas.Save();
			canvas.Translate((float)-leftTopPixel.X, (float)-leftTopPixel.Y);

			if (PWaveGeometryCache != null)
				canvas.DrawPath(PWaveGeometryCache, PWavePaint);
			if (SWaveGeometryCache != null)
			{
				using var sgradPaint = new SKPaint
				{
					IsAntialias = true,
					Style = SKPaintStyle.Fill,
					Shader = SKShader.CreateRadialGradient(
#pragma warning disable CS8604 // Null 参照引数の可能性があります。
					projection.LatLngToPixel(eew.Location, zoom).AsSKPoint(),
#pragma warning restore CS8604 // Null 参照引数の可能性があります。
					SWaveGeometryCache.Bounds.Height / 2,
					new[] { new SKColor(255, 80, 120, 15), new SKColor(255, 80, 120, 80) },
					new[] { .6f, 1f },
					SKShaderTileMode.Clamp
				)
				};
				canvas.DrawPath(SWaveGeometryCache, sgradPaint);
				canvas.DrawPath(SWaveGeometryCache, SWavePaint);
			}
			canvas.Restore();
		}

		protected override void OnTick() => NeedUpdateGeometry = true;
	}
}
