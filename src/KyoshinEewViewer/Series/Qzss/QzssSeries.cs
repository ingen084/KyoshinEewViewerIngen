using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser;
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
	public static SeriesMeta MetaData { get; } = new(typeof(QzssSeries), "qzss", "災危通報", new FontIconSource { Glyph = "\xf7bf", FontFamily = new FontFamily(Utils.IconFontName) }, false, "\"みちびき\" から配信される防災情報を表示します。");

	public ObservableCollection<DCReport> DCReports { get; } = new();

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
			Debug.WriteLine($"DCReport({report.MessageType}): " + report);
			if (report is JmaDCReport jmaDCReport)
			{
				Debug.WriteLine($"  Dc:{jmaDCReport.DisasterCategoryCode} It:{jmaDCReport.InformationType} Rc:{jmaDCReport.ReportClassification}");
				if (!DCReports.Any(x => x.RawData.SequenceEqual(jmaDCReport.RawData)))
					DCReports.Insert(0, jmaDCReport);
			}
			else if (report is OtherOrganizationDCReport otherDCReport)
			{
				Debug.WriteLine($"  Rc:{otherDCReport.ReportClassification} Oc:{otherDCReport.OrganizationCode} Raw:{BitConverter.ToString(otherDCReport.RawData)}");
				if (!DCReports.Any(x => x.RawData.SequenceEqual(otherDCReport.RawData)))
					DCReports.Insert(0, otherDCReport);
			}
		};

		Config = config;
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

		OverlayLayers = new[] {
			CurrentPositionLayer,
		};
	}
	public override void Deactivated() { }
}
