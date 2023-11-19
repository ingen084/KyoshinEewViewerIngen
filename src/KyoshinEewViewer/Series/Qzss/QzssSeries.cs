using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Series.Qzss.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss;

public class QzssSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(QzssSeries), "qzss", "災危通報α", new FontIconSource { Glyph = "\xf7bf", FontFamily = new FontFamily(Utils.IconFontName) }, false, "\"みちびき\" から配信される防災情報を表示します。\nほとんどの機能が未実装です。");

	private ObservableCollection<DCReportGroup> _dcReportGroups = [];
	public ObservableCollection<DCReportGroup> DCReportGroups
	{
		get => _dcReportGroups;
		set => this.RaiseAndSetIfChanged(ref _dcReportGroups, value);
	}

	public QzssSeries(KyoshinEewViewerConfiguration config, SerialConnector connector) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<QzssSeries>();
		MapPadding = new Thickness(260, 0, 0, 0);

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
			if (report is JmaDCReport or OtherOrganizationDCReport)
			{
				foreach (var g in DCReportGroups)
				{
					// すでに受信済みの場合は停止
					if (g.CheckDuplicate(report))
						return;

					// 処理できたら終了
					if (g.TryProcess(report))
						return;
				}

				// 処理できなかった場合は新規追加
				DCReportGroups.Insert(0, report switch
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
					OtherOrganizationDCReport o => new OtherOrganizationReportGroup(o),
					_ => throw new NotImplementedException(),
				});

				if (DCReportGroups.Count > 100)
					DCReportGroups.RemoveAt(DCReportGroups.Count - 1);
			}
		};

		Config = config;

		Config.Qzss.WhenAnyValue(s => s.ShowCurrentPositionInMap).Subscribe(s =>
		{
			if (s)
				OverlayLayers = new[] { CurrentPositionLayer };
			else
				OverlayLayers = null;
		});
	}

	private QzssView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public CurrentPositionLayer CurrentPositionLayer { get; } = new();

	public KyoshinEewViewerConfiguration Config { get; }

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
}
