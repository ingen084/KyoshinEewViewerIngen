using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.TravelTimeTable.Models;
using System;
using System.Windows.Input;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier.Controls;

public partial class HypocenterEditorControl : UserControl
{
	#region IsEnabled
	public new static readonly StyledProperty<bool> IsEnabledProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, bool>(nameof(IsEnabled), defaultValue: false);

	public new bool IsEnabled
	{
		get => GetValue(IsEnabledProperty);
		set => SetValue(IsEnabledProperty, value);
	}
	#endregion

	#region Latitude
	public static readonly StyledProperty<double> LatitudeProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, double>(nameof(Latitude), defaultValue: 35.0);

	public double Latitude
	{
		get => GetValue(LatitudeProperty);
		set => SetValue(LatitudeProperty, value);
	}
	#endregion

	#region Longitude
	public static readonly StyledProperty<double> LongitudeProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, double>(nameof(Longitude), defaultValue: 135.0);

	public double Longitude
	{
		get => GetValue(LongitudeProperty);
		set => SetValue(LongitudeProperty, value);
	}
	#endregion

	#region DepthKm
	public static readonly StyledProperty<int> DepthKmProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, int>(nameof(DepthKm), defaultValue: 10);

	public int DepthKm
	{
		get => GetValue(DepthKmProperty);
		set => SetValue(DepthKmProperty, value);
	}
	#endregion

	#region OriginTimeOffsetSeconds
	public static readonly StyledProperty<double> OriginTimeOffsetSecondsProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, double>(nameof(OriginTimeOffsetSeconds), defaultValue: 0);

	public double OriginTimeOffsetSeconds
	{
		get => GetValue(OriginTimeOffsetSecondsProperty);
		set => SetValue(OriginTimeOffsetSecondsProperty, value);
	}
	#endregion

	#region BaseTime
	public static readonly StyledProperty<DateTime> BaseTimeProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, DateTime>(nameof(BaseTime), defaultValue: DateTime.MinValue);

	public DateTime BaseTime
	{
		get => GetValue(BaseTimeProperty);
		set => SetValue(BaseTimeProperty, value);
	}
	#endregion

	#region OriginTimeText
	public static readonly StyledProperty<string> OriginTimeTextProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, string>(nameof(OriginTimeText), defaultValue: "--:--:--.---");

	public string OriginTimeText
	{
		get => GetValue(OriginTimeTextProperty);
		private set => SetValue(OriginTimeTextProperty, value);
	}
	#endregion

	#region 残差統計プロパティ
	public static readonly StyledProperty<string> ResidualStdDevTextProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, string>(nameof(ResidualStdDevText), defaultValue: "-- s");

	public string ResidualStdDevText
	{
		get => GetValue(ResidualStdDevTextProperty);
		set => SetValue(ResidualStdDevTextProperty, value);
	}

	public static readonly StyledProperty<string> ResidualMeanTextProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, string>(nameof(ResidualMeanText), defaultValue: "-- s");

	public string ResidualMeanText
	{
		get => GetValue(ResidualMeanTextProperty);
		set => SetValue(ResidualMeanTextProperty, value);
	}

	public static readonly StyledProperty<int> UsedStationCountProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, int>(nameof(UsedStationCount), defaultValue: 0);

	public int UsedStationCount
	{
		get => GetValue(UsedStationCountProperty);
		set => SetValue(UsedStationCountProperty, value);
	}

	public static readonly StyledProperty<string> UndetectedPenaltyTextProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, string>(nameof(UndetectedPenaltyText), defaultValue: "--");

	public string UndetectedPenaltyText
	{
		get => GetValue(UndetectedPenaltyTextProperty);
		set => SetValue(UndetectedPenaltyTextProperty, value);
	}

	public static readonly StyledProperty<string> UndetectedDetailsTextProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, string>(nameof(UndetectedDetailsText), defaultValue: "");

	public string UndetectedDetailsText
	{
		get => GetValue(UndetectedDetailsTextProperty);
		set => SetValue(UndetectedDetailsTextProperty, value);
	}
	#endregion

	#region EstimatedHypocenter
	public static readonly StyledProperty<EstimatedHypocenter?> EstimatedHypocenterProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, EstimatedHypocenter?>(nameof(EstimatedHypocenter));

	public EstimatedHypocenter? EstimatedHypocenter
	{
		get => GetValue(EstimatedHypocenterProperty);
		set => SetValue(EstimatedHypocenterProperty, value);
	}
	#endregion

	#region Commands
	public static readonly StyledProperty<ICommand?> ApplyFromEstimatedProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, ICommand?>(nameof(ApplyFromEstimated));

	public ICommand? ApplyFromEstimated
	{
		get => GetValue(ApplyFromEstimatedProperty);
		set => SetValue(ApplyFromEstimatedProperty, value);
	}

	public static readonly StyledProperty<ICommand?> RecalculateResidualsProperty =
		AvaloniaProperty.Register<HypocenterEditorControl, ICommand?>(nameof(RecalculateResiduals));

	public ICommand? RecalculateResiduals
	{
		get => GetValue(RecalculateResidualsProperty);
		set => SetValue(RecalculateResidualsProperty, value);
	}
	#endregion

	public HypocenterEditorControl()
	{
		InitializeComponent();

		// BaseTimeまたはOriginTimeOffsetSecondsが変更されたらOriginTimeTextを更新
		this.GetObservable(BaseTimeProperty).Subscribe(_ => UpdateOriginTimeText());
		this.GetObservable(OriginTimeOffsetSecondsProperty).Subscribe(_ => UpdateOriginTimeText());
	}

	private void UpdateOriginTimeText()
	{
		if (BaseTime == DateTime.MinValue)
		{
			OriginTimeText = "--:--:--.---";
			return;
		}

		var originTime = BaseTime.AddSeconds(OriginTimeOffsetSeconds);
		OriginTimeText = originTime.ToString("HH:mm:ss.fff");
	}

	/// <summary>
	/// 現在の設定から発震時刻を取得する
	/// </summary>
	public DateTime GetOriginTime() => BaseTime.AddSeconds(OriginTimeOffsetSeconds);
}
