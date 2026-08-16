using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Avalonia.Controls;
using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Services.Workflows;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Workflow;

public class QzssTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(QzssEvent);
	[JsonIgnore]
	public override Control DisplayControl => new QzssTriggerControl() { DataContext = this };

	private bool _newSentenceReceived = true;
	public bool NewSentenceReceived
	{
		get => _newSentenceReceived;
		set => SetProperty(ref _newSentenceReceived, value);
	}

	private bool _updateWithMoreAccurate = true;
	public bool ReportGroupCreated
	{
		get => _updateWithMoreAccurate;
		set => SetProperty(ref _updateWithMoreAccurate, value);
	}

	private bool _reportGroupUpdated = true;
	public bool ReportGroupUpdated
	{
		get => _reportGroupUpdated;
		set => SetProperty(ref _reportGroupUpdated, value);
	}

	private bool _nankaiTroughReportGroupCompleted = true;
	public bool NankaiTroughReportCompleted
	{
		get => _nankaiTroughReportGroupCompleted;
		set => SetProperty(ref _nankaiTroughReportGroupCompleted, value);
	}

	// 複数選択のコントロールがないので茶を濁す

	private bool _ashFall = false;
	public bool AshFall
	{
		get => _ashFall;
		set => SetProperty(ref _ashFall, value);
	}

	private bool _dcx = false;
	public bool DCX
	{
		get => _dcx;
		set => SetProperty(ref _dcx, value);
	}

	private bool _eew = false;
	public bool Eew
	{
		get => _eew;
		set => SetProperty(ref _eew, value);
	}

	private bool _flood = false;
	public bool Flood
	{
		get => _flood;
		set => SetProperty(ref _flood, value);
	}

	private bool _hypocenter = false;
	public bool Hypocenter
	{
		get => _hypocenter;
		set => SetProperty(ref _hypocenter, value);
	}

	private bool _marine = false;
	public bool Marine
	{
		get => _marine;
		set => SetProperty(ref _marine, value);
	}

	private bool _nankaiTrough = false;
	public bool NankaiTrough
	{
		get => _nankaiTrough;
		set => SetProperty(ref _nankaiTrough, value);
	}

	private bool _northwestPacificTsunami = false;
	public bool NorthwestPacificTsunami
	{
		get => _northwestPacificTsunami;
		set => SetProperty(ref _northwestPacificTsunami, value);
	}

	private bool _seismicIntensity = false;
	public bool SeismicIntensity
	{
		get => _seismicIntensity;
		set => SetProperty(ref _seismicIntensity, value);
	}

	private bool _tsunami = false;
	public bool Tsunami
	{
		get => _tsunami;
		set => SetProperty(ref _tsunami, value);
	}

	private bool _typhoon = false;
	public bool Typhoon
	{
		get => _typhoon;
		set => SetProperty(ref _typhoon, value);
	}

	private bool _unknown = false;
	public bool Unknown
	{
		get => _unknown;
		set => SetProperty(ref _unknown, value);
	}

	private bool _volcano = false;
	public bool Volcano
	{
		get => _volcano;
		set => SetProperty(ref _volcano, value);
	}

	private bool _weather = false;
	public bool Weather
	{
		get => _weather;
		set => SetProperty(ref _weather, value);
	}

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
