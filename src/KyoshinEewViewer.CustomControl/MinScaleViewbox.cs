using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace KyoshinEewViewer.CustomControl;

/// <summary>
/// ユーザー指定のスケールと最小サイズ制約を組み合わせたスケーリングコントロール。
/// 表示領域が最小サイズ×スケールを下回ると自動的に縮小を開始する。
/// </summary>
public class MinScaleViewbox : Decorator
{
	public static readonly StyledProperty<double> ScaleProperty =
		AvaloniaProperty.Register<MinScaleViewbox, double>(nameof(Scale), 1.0);

	public static readonly StyledProperty<double> MinViewWidthProperty =
		AvaloniaProperty.Register<MinScaleViewbox, double>(nameof(MinViewWidth));

	public static readonly StyledProperty<double> MinViewHeightProperty =
		AvaloniaProperty.Register<MinScaleViewbox, double>(nameof(MinViewHeight));

	private double _effectiveScale = 1.0;
	public static readonly DirectProperty<MinScaleViewbox, double> EffectiveScaleProperty =
		AvaloniaProperty.RegisterDirect<MinScaleViewbox, double>(
			nameof(EffectiveScale),
			o => o.EffectiveScale);

	/// <summary>
	/// ユーザー指定のスケール倍率
	/// </summary>
	public double Scale
	{
		get => GetValue(ScaleProperty);
		set => SetValue(ScaleProperty, value);
	}

	/// <summary>
	/// 論理的な最小幅。実質最小幅は MinViewWidth × Scale となる。
	/// </summary>
	public double MinViewWidth
	{
		get => GetValue(MinViewWidthProperty);
		set => SetValue(MinViewWidthProperty, value);
	}

	/// <summary>
	/// 論理的な最小高さ。実質最小高さは MinViewHeight × Scale となる。
	/// </summary>
	public double MinViewHeight
	{
		get => GetValue(MinViewHeightProperty);
		set => SetValue(MinViewHeightProperty, value);
	}

	/// <summary>
	/// 現在の実効スケール（ユーザースケール × 自動縮小率）。読み取り専用。
	/// </summary>
	public double EffectiveScale
	{
		get => _effectiveScale;
		private set => SetAndRaise(EffectiveScaleProperty, ref _effectiveScale, value);
	}

	static MinScaleViewbox()
	{
		AffectsMeasure<MinScaleViewbox>(ScaleProperty, MinViewWidthProperty, MinViewHeightProperty);
		AffectsArrange<MinScaleViewbox>(ScaleProperty, MinViewWidthProperty, MinViewHeightProperty);
	}

	private double ComputeEffectiveScale(Size availableSize)
	{
		var userScale = Math.Max(Scale, 0.001);
		var effectiveMinW = MinViewWidth * userScale;
		var effectiveMinH = MinViewHeight * userScale;

		var autoScaleX = effectiveMinW > 0 && availableSize.Width < effectiveMinW
			? availableSize.Width / effectiveMinW : 1.0;
		var autoScaleY = effectiveMinH > 0 && availableSize.Height < effectiveMinH
			? availableSize.Height / effectiveMinH : 1.0;

		return userScale * Math.Min(autoScaleX, autoScaleY);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (Child is not { } child)
			return default;

		var scale = ComputeEffectiveScale(availableSize);

		child.Measure(new Size(
			double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width / scale,
			double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height / scale
		));

		return new Size(
			child.DesiredSize.Width * scale,
			child.DesiredSize.Height * scale
		);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		if (Child is not { } child)
			return finalSize;

		var scale = ComputeEffectiveScale(finalSize);
		EffectiveScale = scale;

		child.RenderTransformOrigin = RelativePoint.TopLeft;
		child.RenderTransform = new ScaleTransform(scale, scale);
		child.Arrange(new Rect(new Size(
			finalSize.Width / scale,
			finalSize.Height / scale
		)));

		return finalSize;
	}
}
