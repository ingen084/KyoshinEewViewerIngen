using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using KyoshinEewViewer.Map;
using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.CustomControl
{
	public class IntensityIcon : Control, ICustomDrawOperation
	{
		private JmaIntensity? intensity;
		public static readonly DirectProperty<IntensityIcon, JmaIntensity?> IntensityProperty =
			AvaloniaProperty.RegisterDirect<IntensityIcon, JmaIntensity?>(
				nameof(Intensity),
				o => o.intensity,
				(o, v) =>
				{
					o.intensity = v;
					o.InvalidateVisual();
				}
			);
		public JmaIntensity? Intensity
		{
			get => intensity;
			set => SetAndRaise(IntensityProperty, ref intensity, value);
		}

		private bool circleMode;
		public static readonly DirectProperty<IntensityIcon, bool> CircleModeProperty =
			AvaloniaProperty.RegisterDirect<IntensityIcon, bool>(
				nameof(CircleMode),
				o => o.circleMode,
				(o, v) =>
				{
					o.circleMode = v;
					o.InvalidateVisual();
				});
		public bool CircleMode
		{
			get => circleMode;
			set => SetAndRaise(CircleModeProperty, ref circleMode, value);
		}

		private bool wideMode;
		public static readonly DirectProperty<IntensityIcon, bool> WideModeProperty =
			AvaloniaProperty.RegisterDirect<IntensityIcon, bool>(
				nameof(WideMode),
				o => o.wideMode,
				(o, v) =>
				{
					o.wideMode = v;
					o.InvalidateMeasure();
				});
		public bool WideMode
		{
			get => wideMode;
			set => SetAndRaise(WideModeProperty, ref wideMode, value);
		}

		protected override void OnInitialized()
		{
			base.OnInitialized();

			if (!FixedObjectRenderer.PaintCacheInitalized)
				FixedObjectRenderer.UpdateIntensityPaintCache(this);
		}

		public bool Equals(ICustomDrawOperation? other) => false;
		public bool HitTest(Point p) => false;

		public void Render(IDrawingContextImpl context)
		{
			var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
			if (canvas == null)
				return;
			canvas.Save();

			var size = Math.Min(DesiredSize.Width, DesiredSize.Height);
			canvas.DrawIntensity(Intensity ?? JmaIntensity.Error, new PointD(), (float)size, circle: CircleMode, wide: WideMode);

			canvas.Restore();
		}
		public override void Render(DrawingContext context) => context.Custom(this);

		public void Dispose() { }

		protected override Size MeasureOverride(Size availableSize)
		{
			var w = availableSize.Width;
			var h = availableSize.Height;

			if (h > w)
				return new Size(w, WideMode ? w * FixedObjectRenderer.INTENSITY_WIDE_SCALE : w);
			return new Size(WideMode ? h / FixedObjectRenderer.INTENSITY_WIDE_SCALE : h, h);
		}
	}
}
