using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.CustomControl;

public class IntensityIcon : Control, ICustomDrawOperation
{
	private JmaIntensity? _intensity;
	public static readonly DirectProperty<IntensityIcon, JmaIntensity?> IntensityProperty =
		AvaloniaProperty.RegisterDirect<IntensityIcon, JmaIntensity?>(
			nameof(Intensity),
			o => o._intensity,
			(o, v) =>
			{
				o._intensity = v;
				o.InvalidateVisual();
			}
		);
	public JmaIntensity? Intensity
	{
		get => _intensity;
		set => SetAndRaise(IntensityProperty, ref _intensity, value);
	}

	private bool _circleMode;
	public static readonly DirectProperty<IntensityIcon, bool> CircleModeProperty =
		AvaloniaProperty.RegisterDirect<IntensityIcon, bool>(
			nameof(CircleMode),
			o => o._circleMode,
			(o, v) =>
			{
				o._circleMode = v;
				o.InvalidateVisual();
			});
	public bool CircleMode
	{
		get => _circleMode;
		set => SetAndRaise(CircleModeProperty, ref _circleMode, value);
	}

	private bool _wideMode;
	public static readonly DirectProperty<IntensityIcon, bool> WideModeProperty =
		AvaloniaProperty.RegisterDirect<IntensityIcon, bool>(
			nameof(WideMode),
			o => o._wideMode,
			(o, v) =>
			{
				o._wideMode = v;
				o.InvalidateMeasure();
				o.InvalidateVisual();
			});
	public bool WideMode
	{
		get => _wideMode;
		set => SetAndRaise(WideModeProperty, ref _wideMode, value);
	}

	private bool _cornerRound;
	public static readonly DirectProperty<IntensityIcon, bool> CornerRoundProperty =
		AvaloniaProperty.RegisterDirect<IntensityIcon, bool>(
			nameof(CornerRound),
			o => o._cornerRound,
			(o, v) =>
			{
				o._cornerRound = v;
				o.InvalidateMeasure();
				o.InvalidateVisual();
			});
	public bool CornerRound
	{
		get => _cornerRound;
		set => SetAndRaise(CornerRoundProperty, ref _cornerRound, value);
	}

	protected override void OnInitialized()
	{
		base.OnInitialized();

		if (!FixedObjectRenderer.PaintCacheInitialized)
			FixedObjectRenderer.UpdateIntensityPaintCache(this);
	}

	public bool Equals(ICustomDrawOperation? other) => false;
	public bool HitTest(Point p) => false;

	public void Render(ImmediateDrawingContext context)
	{
		if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var leaseFeature))
			return;
		using var lease = leaseFeature.Lease();
		var canvas = lease.SkCanvas;
		canvas.Save();

		var size = Math.Min(DesiredSize.Width, DesiredSize.Height);
		canvas.DrawIntensity(Intensity ?? JmaIntensity.Error, new SkiaSharp.SKPoint(), (float)size, circle: CircleMode, wide: WideMode, round: CornerRound);

		canvas.Restore();
	}
	public override void Render(DrawingContext context) => context.Custom(this);

	public void Dispose() => GC.SuppressFinalize(this);

	protected override Size MeasureOverride(Size availableSize)
	{
		var w = availableSize.Width;
		var h = availableSize.Height;

		if (h > w)
			return new Size(w, WideMode ? w * FixedObjectRenderer.IntensityWideScale : w);
		return new Size(WideMode ? h / FixedObjectRenderer.IntensityWideScale : h, h);
	}
}
