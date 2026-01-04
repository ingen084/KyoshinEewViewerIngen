using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.ShakeDetection;
using ReactiveUI;
using System.Reactive;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier.Controls;

public partial class ParameterEditorControl : UserControl
{
	public static readonly StyledProperty<ShakeDetectionParameters> ParametersProperty =
		AvaloniaProperty.Register<ParameterEditorControl, ShakeDetectionParameters>(
			nameof(Parameters),
			defaultValue: ShakeDetectionParameters.Default);

	public ShakeDetectionParameters Parameters
	{
		get => GetValue(ParametersProperty);
		set => SetValue(ParametersProperty, value);
	}

	#region パラメータプロパティ
	public double MaxSearchDistance
	{
		get => Parameters.MaxSearchDistance;
		set => Parameters = Parameters with { MaxSearchDistance = value };
	}

	public double PeakWeightDistance
	{
		get => Parameters.PeakWeightDistance;
		set => Parameters = Parameters with { PeakWeightDistance = value };
	}

	public double IsolatedThreshold
	{
		get => Parameters.IsolatedThreshold;
		set => Parameters = Parameters with { IsolatedThreshold = value };
	}

	public double IsolatedDetectionDiff
	{
		get => Parameters.IsolatedDetectionDiff;
		set => Parameters = Parameters with { IsolatedDetectionDiff = value };
	}

	public double ScoreThresholdRatio
	{
		get => Parameters.ScoreThresholdRatio;
		set => Parameters = Parameters with { ScoreThresholdRatio = value };
	}

	public double ScoreIntensityOffset
	{
		get => Parameters.ScoreIntensityOffset;
		set => Parameters = Parameters with { ScoreIntensityOffset = value };
	}

	public double MinDetectionDiff
	{
		get => Parameters.MinDetectionDiff;
		set => Parameters = Parameters with { MinDetectionDiff = value };
	}

	public double NoChangePenaltyFactor
	{
		get => Parameters.NoChangePenaltyFactor;
		set => Parameters = Parameters with { NoChangePenaltyFactor = value };
	}

	public int MaxNearPoints
	{
		get => Parameters.MaxNearPoints;
		set => Parameters = Parameters with { MaxNearPoints = value };
	}

	public double StrongerMergeDistance
	{
		get => Parameters.StrongerMergeDistance;
		set => Parameters = Parameters with { StrongerMergeDistance = value };
	}

	public double StrongMergeDistance
	{
		get => Parameters.StrongMergeDistance;
		set => Parameters = Parameters with { StrongMergeDistance = value };
	}

	public double MediumMergeDistance
	{
		get => Parameters.MediumMergeDistance;
		set => Parameters = Parameters with { MediumMergeDistance = value };
	}

	public double WeakMergeDistance
	{
		get => Parameters.WeakMergeDistance;
		set => Parameters = Parameters with { WeakMergeDistance = value };
	}

	public double WeakerMergeDistance
	{
		get => Parameters.WeakerMergeDistance;
		set => Parameters = Parameters with { WeakerMergeDistance = value };
	}

	public int StrongerSeconds
	{
		get => Parameters.StrongerSeconds;
		set => Parameters = Parameters with { StrongerSeconds = value };
	}

	public int StrongSeconds
	{
		get => Parameters.StrongSeconds;
		set => Parameters = Parameters with { StrongSeconds = value };
	}

	public int MediumSeconds
	{
		get => Parameters.MediumSeconds;
		set => Parameters = Parameters with { MediumSeconds = value };
	}

	public int WeakSeconds
	{
		get => Parameters.WeakSeconds;
		set => Parameters = Parameters with { WeakSeconds = value };
	}

	public int WeakerSeconds
	{
		get => Parameters.WeakerSeconds;
		set => Parameters = Parameters with { WeakerSeconds = value };
	}

	public int WeakerConfirmPointCount
	{
		get => Parameters.WeakerConfirmPointCount;
		set => Parameters = Parameters with { WeakerConfirmPointCount = value };
	}

	public int WeakConfirmPointCount
	{
		get => Parameters.WeakConfirmPointCount;
		set => Parameters = Parameters with { WeakConfirmPointCount = value };
	}
	#endregion

	public ParameterEditorControl()
	{
		InitializeComponent();
	}

	public void ResetToDefault()
	{
		Parameters = ShakeDetectionParameters.Default;
	}
}
