using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.Qzss.Events;
using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Series.Qzss.Services;
using KyoshinEewViewer.Series.Qzss.SettingPages;
using KyoshinEewViewer.Series.Qzss.Workflow;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss;

public class QzssSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(QzssSeries), "qzss", "災危通報", new FontIconSource { Glyph = "\xf7bf", FontFamily = new FontFamily(Utils.IconFontName) }, false, "\"みちびき\" から配信される防災情報を表示します。");

	private MapData? MapData { get; set; }

	private SoundCategory SoundCategory { get; } = new("Qzss", "災危通報");
	private Sound ReceivedSound { get; }
	private Sound GroupAddedSound { get; }
	private Sound NankaiTroughCompletedSound { get; }

	private WorkflowService WorkflowService { get; }

	private Thickness OffsetPadding { get; } = new(260, 0, 0, 0);
	private IDisposable? _mapDisplayParameterSubscription;
	private IDisposable? _navigationRequestSubscription;

	public QzssSeries(KyoshinEewViewerConfiguration config, SerialConnector connector, SoundPlayerService soundPlayer, WorkflowService workflowService) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<QzssSeries>();
		MapDisplayParameter = new()
		{
			Padding = OffsetPadding
		};

		WorkflowService = workflowService;

		ReceivedSound = soundPlayer.RegisterSound(SoundCategory, "Received", "新規情報の受信", "同時発表の情報に統合された場合でも鳴動しますが、完全に同じ情報では鳴動しません。");
		GroupAddedSound = soundPlayer.RegisterSound(SoundCategory, "GroupAdded", "新規グループ受信", "同時発表の情報と統合された場合には鳴動しません。");
		NankaiTroughCompletedSound = soundPlayer.RegisterSound(SoundCategory, "NankaiTroughCompleted", "南海トラフ情報受信完了", "南海トラフに関する情報の受信が完了した場合に鳴動します。");

		Connector = connector;
		Connector.WhenAnyValue(s => s.CurrentLocation).Subscribe(s =>
		{
			if (s == null)
				return;
			CurrentPositionLayer.Location = s;
		});
		Connector.DCReportReceived += report =>
		{
			LastDCReportReceivedTime = Connector.LastReceivedTime;
			ProcessDCReport(report);
		};
		MessageBus.Current.Listen<ProcessManualDCReportRequested>().Subscribe(s => ProcessDCReport(s.Report));

		Config = config;

		Config.Qzss.WhenAnyValue(s => s.ShowCurrentPositionInMap).Subscribe(s => UpdateDisplay());

		this.WhenAnyValue(s => s.SelectedDCReportGroup).Subscribe(g =>
		{
			_navigationRequestSubscription?.Dispose();
			_navigationRequestSubscription = null;
			_mapDisplayParameterSubscription?.Dispose();
			_mapDisplayParameterSubscription = null;

			UpdateDisplay();

			if (g == null)
				return;
			_mapDisplayParameterSubscription = g.WhenAnyValue(s => s.MapDisplayParameter).Subscribe(s => UpdateDisplay());
			_navigationRequestSubscription = g.WhenAnyValue(s => s.MapNavigationRequest).Subscribe(s => UpdateDisplay());
		});
	}

	private QzssView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [
		new QzssSettingPage(Config, Connector),
	];

	public CurrentPositionLayer CurrentPositionLayer { get; } = new();

	public KyoshinEewViewerConfiguration Config { get; }


	private ObservableCollection<DCReportGroup> _dcReportGroups = [];
	public ObservableCollection<DCReportGroup> DCReportGroups
	{
		get => _dcReportGroups;
		set => this.RaiseAndSetIfChanged(ref _dcReportGroups, value);
	}

	private DCReportGroup? _selectedDCReportGroup;
	public DCReportGroup? SelectedDCReportGroup
	{
		get => _selectedDCReportGroup;
		set => this.RaiseAndSetIfChanged(ref _selectedDCReportGroup, value);
	}

	public SerialConnector Connector { get; }

	private DateTime? _lastDCReportReceivedTime;
	public DateTime? LastDCReportReceivedTime
	{
		get => _lastDCReportReceivedTime;
		set => this.RaiseAndSetIfChanged(ref _lastDCReportReceivedTime, value);
	}

	public override void Initialize()
	{
		MessageBus.Current.Listen<MapLoaded>().Subscribe(x => MapData = x.Data);
	}

	public override void Activating()
	{
		if (_control != null)
			return;
		_control = new QzssView
		{
			DataContext = this,
		};
	}
	public override void Deactivated() { }

	public void ProcessDCReport(DCReport report)
	{
		// 43/44 以外は無視
		if (report is not JmaDCReport && report is not OtherOrganizationDCReport)
			return;

		// 他機関の情報を無視
		if (Config.Qzss.IgnoreOtherOrganizationReport && report is OtherOrganizationDCReport)
			return;
		// 訓練・試験を無視
		if (Config.Qzss.IgnoreTrainingOrTestReport && (
			(report is JmaDCReport j && j.ReportClassification == ReportClassification.TrainingOrTest) ||
			(report is OtherOrganizationDCReport o && o.ReportClassification == ReportClassification.TrainingOrTest)
		))
			return;

		var sentence = new string(report.RawData.SelectMany(d => d.ToString("X02")).ToArray()[..^1]);
		foreach (var g in DCReportGroups)
		{
			// すでに受信済みの場合は停止
			if (g.CheckDuplicate(report))
				return;

			// 処理できたら終了
			if (g.TryProcess(report))
			{
				WorkflowService.PublishEvent(new QzssEvent(this, QzssEventType.NewSentenceReceived, sentence, g));
				WorkflowService.PublishEvent(new QzssEvent(this, QzssEventType.ReportGroupUpdated, sentence, g));

				SelectedDCReportGroup = g;
				UpdateDisplay();

				// 音を鳴らす
				if (g is NankaiTroughEarthquakeReportGroup n && n.TotalPage <= n.CurrentProgress)
				{
					WorkflowService.PublishEvent(new QzssEvent(this, QzssEventType.NankaiTroughReportCompleted, sentence, g));
					if (!NankaiTroughCompletedSound.Play())
						ReceivedSound.Play();
				}
				else
					ReceivedSound.Play();
				return;
			}
		}

		// 処理できなかった場合は新規追加
		DCReportGroup newGroup = report switch
		{
			EewReport e => new EewReportGroup(e, MapData),
			SeismicIntensityReport s => new SeismicIntensityReportGroup(s, MapData),
			HypocenterReport h => new HypocenterReportGroup(h),
			NankaiTroughEarthquakeReport n => new NankaiTroughEarthquakeReportGroup(n),
			TsunamiReport t => new TsunamiReportGroup(t, MapData),
			NorthwestPacificTsunamiReport n => new NorthwestPacificTsunamiReportGroup(n),
			VolcanoReport v => new VolcanoReportGroup(v),
			AshFallReport a => new AshFallReportGroup(a, MapData),
			WeatherReport w => new WeatherReportGroup(w, MapData),
			FloodReport f => new FloodReportGroup(f),
			TyphoonReport t => new TyphoonReportGroup(t),
			MarineReport m => new MarineReportGroup(m),
			OtherOrganizationDCReport r => new DCXReportGroup(r),
			_ => new UnknownReportGroup(report),
		};
		WorkflowService.PublishEvent(new QzssEvent(this, QzssEventType.NewSentenceReceived, sentence, newGroup));
		WorkflowService.PublishEvent(new QzssEvent(this, QzssEventType.ReportGroupCreated, sentence, newGroup));

		DCReportGroups.Insert(0, newGroup);
		SelectedDCReportGroup = newGroup;

		if (DCReportGroups.Count > 500)
			DCReportGroups.RemoveAt(DCReportGroups.Count - 1);

		if (!GroupAddedSound.Play())
			ReceivedSound.Play();
	}

	public void UpdateDisplay()
	{
		if (SelectedDCReportGroup == null)
		{
			MapDisplayParameter = new()
			{
				Padding = OffsetPadding,
				OverlayLayers = Config.Qzss.ShowCurrentPositionInMap ? [CurrentPositionLayer] : null,
			};
			MapNavigationRequest = null;
			return;
		}

		var overlayLayers = SelectedDCReportGroup.MapDisplayParameter.OverlayLayers;
		if (Config.Qzss.ShowCurrentPositionInMap)
		{
			if (overlayLayers is { } l)
				overlayLayers = [.. l, CurrentPositionLayer];
			else
				overlayLayers = [CurrentPositionLayer];
		}

		MapDisplayParameter = SelectedDCReportGroup.MapDisplayParameter with
		{
			Padding = OffsetPadding + SelectedDCReportGroup.MapDisplayParameter.Padding,
			OverlayLayers = overlayLayers,
		};
		MapNavigationRequest = SelectedDCReportGroup.MapNavigationRequest;
	}
}
