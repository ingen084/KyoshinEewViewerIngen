using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using KyoshinEewViewer.Core;
using System;

namespace KyoshinEewViewer.CustomControl;

public class LpgmIntensityIcon : Control
{
	private LpgmIntensityIconRenderOperation RenderOperation { get; } = new LpgmIntensityIconRenderOperation();

	public static readonly DirectProperty<LpgmIntensityIcon, LpgmIntensity?> IntensityProperty =
		AvaloniaProperty.RegisterDirect<LpgmIntensityIcon, LpgmIntensity?>(
			nameof(Intensity),
			o => o.RenderOperation.Intensity,
			(o, v) =>
			{
				o.RenderOperation.Intensity = v;
				o.InvalidateVisual();
			}
		);
	public LpgmIntensity? Intensity
	{
		get => RenderOperation.Intensity;
		set => SetAndRaise(IntensityProperty, ref RenderOperation.Intensity, value);
	}

	public static readonly DirectProperty<LpgmIntensityIcon, bool> CircleModeProperty =
		AvaloniaProperty.RegisterDirect<LpgmIntensityIcon, bool>(
			nameof(CircleMode),
			o => o.RenderOperation.CircleMode,
			(o, v) =>
			{
				o.RenderOperation.CircleMode = v;
				o.InvalidateVisual();
			});
	public bool CircleMode
	{
		get => RenderOperation.CircleMode;
		set => SetAndRaise(CircleModeProperty, ref RenderOperation.CircleMode, value);
	}

	public static readonly DirectProperty<LpgmIntensityIcon, bool> WideModeProperty =
		AvaloniaProperty.RegisterDirect<LpgmIntensityIcon, bool>(
			nameof(WideMode),
			o => o.RenderOperation.WideMode,
			(o, v) =>
			{
				o.RenderOperation.WideMode = v;
				o.InvalidateMeasure();
				o.InvalidateVisual();
			});
	public bool WideMode
	{
		get => RenderOperation.WideMode;
		set => SetAndRaise(WideModeProperty, ref RenderOperation.WideMode, value);
	}

	public static readonly DirectProperty<LpgmIntensityIcon, bool> CornerRoundProperty =
		AvaloniaProperty.RegisterDirect<LpgmIntensityIcon, bool>(
			nameof(CornerRound),
			o => o.RenderOperation.CornerRound,
			(o, v) =>
			{
				o.RenderOperation.CornerRound = v;
				o.InvalidateMeasure();
				o.InvalidateVisual();
			});
	public bool CornerRound
	{
		get => RenderOperation.CornerRound;
		set => SetAndRaise(CornerRoundProperty, ref RenderOperation.CornerRound, value);
	}

	public static readonly DirectProperty<LpgmIntensityIcon, bool> BorderProperty =
		AvaloniaProperty.RegisterDirect<LpgmIntensityIcon, bool>(
			nameof(Border),
			o => o.RenderOperation.Border,
			(o, v) =>
			{
				o.RenderOperation.Border = v;
				o.InvalidateVisual();
			});
	public bool Border
	{
		get => RenderOperation.Border;
		set => SetAndRaise(BorderProperty, ref RenderOperation.Border, value);
	}

	protected override void OnInitialized()
	{
		base.OnInitialized();

		if (!FixedObjectRenderer.PaintCacheInitialized)
			FixedObjectRenderer.UpdateIntensityPaintCache(this);
	}

	public override void Render(DrawingContext context) => context.Custom(RenderOperation);

	protected override void OnSizeChanged(SizeChangedEventArgs e)
	{
		base.OnSizeChanged(e);
		RenderOperation.Bounds = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
	}
	protected override Size MeasureOverride(Size availableSize)
	{
		var w = availableSize.Width;
		var h = availableSize.Height;

		if (h > w)
			return new Size(w, WideMode ? w * FixedObjectRenderer.IntensityWideScale : w);
		return new Size(WideMode ? h / FixedObjectRenderer.IntensityWideScale : h, h);
	}

	public class LpgmIntensityIconRenderOperation : ICustomDrawOperation
	{
		public LpgmIntensity? Intensity;

		public bool CircleMode;

		public bool WideMode;

		public bool CornerRound;

		public bool Border;

		public Rect Bounds { get; set; }

		public void Dispose() => GC.SuppressFinalize(this);
		public bool Equals(ICustomDrawOperation? other) => this == other;
		public bool HitTest(Point p) => false;
		public void Render(ImmediateDrawingContext context)
		{
			if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var leaseFeature))
				return;
			using var lease = leaseFeature.Lease();
			var canvas = lease.SkCanvas;

			var size = Math.Min(Bounds.Width, Bounds.Height);
			canvas.DrawLpgmIntensity(Intensity ?? LpgmIntensity.Error, new SkiaSharp.SKPoint(), (float)size, circle: CircleMode, wide: WideMode, round: CornerRound, border: Border);
		}
	}
}
