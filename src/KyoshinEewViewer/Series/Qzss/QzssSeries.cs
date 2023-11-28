using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Series.Qzss.Events;
using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Series.Qzss.Services;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;

namespace KyoshinEewViewer.Series.Qzss;

public class QzssSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(QzssSeries), "qzss", "災危通報α", new FontIconSource { Glyph = "\xf7bf", FontFamily = new FontFamily(Utils.IconFontName) }, false, "\"みちびき\" から配信される防災情報を表示します。\nほとんどの機能が未実装です。");

	private SoundCategory SoundCategory { get; } = new("Qzss", "災危通報");
	private Sound ReceivedSound { get; }
	private Sound GroupAddedSound { get; }
	private Sound NankaiTroughCompletedSound { get; }

	public QzssSeries(KyoshinEewViewerConfiguration config, SerialConnector connector, SoundPlayerService soundPlayer) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<QzssSeries>();
		MapPadding = new Thickness(260, 0, 0, 0);

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

		Config.Qzss.WhenAnyValue(s => s.ShowCurrentPositionInMap).Subscribe(s =>
		{
			if (s)
				OverlayLayers = new[] { CurrentPositionLayer };
			else
				OverlayLayers = null;
		});

		this.WhenAnyValue(s => s.SelectedDCReportGroup).Subscribe(s =>
		{
			// TODO: MainView 更新処理
		});
	}

	private QzssView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

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

		foreach (var g in DCReportGroups)
		{
			// すでに受信済みの場合は停止
			if (g.CheckDuplicate(report))
				return;

			// 処理できたら終了
			if (g.TryProcess(report))
			{
				SelectedDCReportGroup = g;

				// 音を鳴らす
				if (g is NankaiTroughEarthquakeReportGroup n && n.TotalPage <= n.CurrentProgress)
				{
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
			EewReport e => new EewReportGroup(e),
			SeismicIntensityReport s => new SeismicIntensityReportGroup(s),
			HypocenterReport h => new HypocenterReportGroup(h),
			NankaiTroughEarthquakeReport n => new NankaiTroughEarthquakeReportGroup(n),
			TsunamiReport t => new TsunamiReportGroup(t),
			NorthwestPacificTsunamiReport n => new NorthwestPacificTsunamiReportGroup(n),
			VolcanoReport v => new VolcanoReportGroup(v),
			AshFallReport a => new AshFallReportGroup(a),
			WeatherReport w => new WeatherReportGroup(w),
			FloodReport f => new FloodReportGroup(f),
			TyphoonReport t => new TyphoonReportGroup(t),
			MarineReport m => new MarineReportGroup(m),
			OtherOrganizationDCReport r => new OtherOrganizationReportGroup(r),
			_ => new UnknownReportGroup(report),
		};
		DCReportGroups.Insert(0, newGroup);
		SelectedDCReportGroup = newGroup;

		if (DCReportGroups.Count > 500)
			DCReportGroups.RemoveAt(DCReportGroups.Count - 1);

		if (!GroupAddedSound.Play())
			ReceivedSound.Play();
	}
}
