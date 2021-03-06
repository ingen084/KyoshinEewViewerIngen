﻿using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.CustomControls
{
	public class IntensityIcon : FrameworkElement
	{
		public JmaIntensity? Intensity
		{
			get => (JmaIntensity?)GetValue(IntensityProperty);
			set => SetValue(IntensityProperty, value);
		}
		public static readonly DependencyProperty IntensityProperty =
			DependencyProperty.Register("Intensity", typeof(JmaIntensity?), typeof(IntensityIcon), new PropertyMetadata(JmaIntensity.Unknown, (s, e) => (s as FrameworkElement)?.InvalidateVisual()));

		public bool CircleMode
		{
			get => (bool)GetValue(CircleModeProperty);
			set => SetValue(CircleModeProperty, value);
		}
		public static readonly DependencyProperty CircleModeProperty =
			DependencyProperty.Register("CircleMode", typeof(bool), typeof(IntensityIcon), new PropertyMetadata(false, (s, e) => (s as FrameworkElement)?.InvalidateVisual()));

		public bool WideMode
		{
			get => (bool)GetValue(WideModeProperty);
			set => SetValue(WideModeProperty, value);
		}
		public static readonly DependencyProperty WideModeProperty =
			DependencyProperty.Register("WideMode", typeof(bool), typeof(IntensityIcon), new PropertyMetadata(false, (s, e) => (s as FrameworkElement)?.InvalidateMeasure()));

		protected override void OnRender(DrawingContext drawingContext)
		{
			var size = Math.Min(RenderSize.Width, RenderSize.Height);
			drawingContext.DrawIntensity(Intensity ?? JmaIntensity.Error, new Point(), size, circle: CircleMode, wide: WideMode);
		}
		protected override Size MeasureOverride(Size availableSize)
		{
			var size = Math.Min(WideMode ? availableSize.Width * FixedObjectRenderer.INTENSITY_WIDE_SCALE : availableSize.Width, availableSize.Height);
			return new Size(WideMode ? size * .8 : size, size);
		}
	}
}
