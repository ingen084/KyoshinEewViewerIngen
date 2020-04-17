﻿using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.CustomControls
{
	public class IntensityIcon : FrameworkElement
	{
		public JmaIntensity Intensity
		{
			get => (JmaIntensity)GetValue(MyPropertyProperty);
			set => SetValue(MyPropertyProperty, value);
		}
		public static readonly DependencyProperty MyPropertyProperty =
			DependencyProperty.Register("Intensity", typeof(JmaIntensity), typeof(IntensityIcon), new PropertyMetadata(JmaIntensity.Unknown));

		public bool CircleMode
		{
			get => (bool)GetValue(CircleModeProperty);
			set => SetValue(CircleModeProperty, value);
		}
		public static readonly DependencyProperty CircleModeProperty =
			DependencyProperty.Register("CircleMode", typeof(bool), typeof(IntensityIcon), new PropertyMetadata(false, (s, e) => (s as FrameworkElement)?.InvalidateVisual()));

		protected override void OnRender(DrawingContext drawingContext)
		{
			var size = Math.Min(RenderSize.Width, RenderSize.Height);
			drawingContext.DrawIntensity(Intensity, new Point(), size, circle: CircleMode);
		}
		protected override Size MeasureOverride(Size availableSize)
		{
			var size = Math.Min(availableSize.Width, availableSize.Height);
			return new Size(size, size);
		}
	}
}