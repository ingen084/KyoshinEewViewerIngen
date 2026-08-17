using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Avalonia.Controls;
using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Services.Workflows;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Workflow;

public partial class QzssTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(QzssEvent);
	[JsonIgnore]
	public override Control DisplayControl => new QzssTriggerControl() { DataContext = this };

	[ObservableProperty]
	public partial bool NewSentenceReceived { get; set; } = true;

	[ObservableProperty]
	public partial bool ReportGroupCreated { get; set; } = true;

	[ObservableProperty]
	public partial bool ReportGroupUpdated { get; set; } = true;

	[ObservableProperty]
	public partial bool NankaiTroughReportCompleted { get; set; } = true;

	// 複数選択のコントロールがないので茶を濁す

	[ObservableProperty]
	public partial bool AshFall { get; set; } = false;

	[ObservableProperty]
	public partial bool DCX { get; set; } = false;

	[ObservableProperty]
	public partial bool Eew { get; set; } = false;

	[ObservableProperty]
	public partial bool Flood { get; set; } = false;

	[ObservableProperty]
	public partial bool Hypocenter { get; set; } = false;

	[ObservableProperty]
	public partial bool Marine { get; set; } = false;

	[ObservableProperty]
	public partial bool NankaiTrough { get; set; } = false;

	[ObservableProperty]
	public partial bool NorthwestPacificTsunami { get; set; } = false;

	[ObservableProperty]
	public partial bool SeismicIntensity { get; set; } = false;

	[ObservableProperty]
	public partial bool Tsunami { get; set; } = false;

	[ObservableProperty]
	public partial bool Typhoon { get; set; } = false;

	[ObservableProperty]
	public partial bool Unknown { get; set; } = false;

	[ObservableProperty]
	public partial bool Volcano { get; set; } = false;

	[ObservableProperty]
	public partial bool Weather { get; set; } = false;

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not QzssEvent qzssEvent)
			return false;
		return (qzssEvent.EventSubType switch
		{
			QzssEventType.NewSentenceReceived => NewSentenceReceived,
			QzssEventType.ReportGroupCreated => ReportGroupCreated,
			QzssEventType.ReportGroupUpdated => ReportGroupUpdated,
			QzssEventType.NankaiTroughReportCompleted => NankaiTroughReportCompleted,
			_ => false
		}) && (qzssEvent.Information switch
		{
			null => true,
			AshFallReportGroup => AshFall,
			DCXReportGroup => DCX,
			EewReportGroup => Eew,
			FloodReportGroup => Flood,
			HypocenterReportGroup => Hypocenter,
			MarineReportGroup => Marine,
			NankaiTroughEarthquakeReportGroup => NankaiTrough,
			NorthwestPacificTsunamiReportGroup => NorthwestPacificTsunami,
			SeismicIntensityReportGroup => SeismicIntensity,
			TsunamiReportGroup => Tsunami,
			TyphoonReportGroup => Typhoon,
			UnknownReportGroup => Unknown,
			VolcanoReportGroup => Volcano,
			WeatherReportGroup => Weather,
			_ => false
		});
	}

	// TODO 拡充させる
	public override WorkflowEvent CreateTestEvent()
		=> new QzssEvent(null, QzssEventType.NewSentenceReceived, "0123456789AB", null!);
}
