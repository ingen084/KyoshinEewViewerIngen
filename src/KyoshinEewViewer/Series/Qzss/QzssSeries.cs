using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Qzss.Events;
using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Series.Qzss.Services;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KyoshinEewViewer.Series.Qzss;

public class QzssSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(QzssSeries), "qzss", "災危通報α", new FontIconSource { Glyph = "\xf7bf", FontFamily = new FontFamily(Utils.IconFontName) }, false, "\"みちびき\" から配信される防災情報を表示します。\nほとんどの機能が未実装です。");

	private MapData? MapData { get; set; }

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
			UpdateDisplay();
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

		foreach (var g in DCReportGroups)
		{
			// すでに受信済みの場合は停止
			if (g.CheckDuplicate(report))
				return;

			// 処理できたら終了
			if (g.TryProcess(report))
			{
				SelectedDCReportGroup = g;
				UpdateDisplay();

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

	private readonly int[] EewAreaTable = [9011, 9012, 9013, 9014, 9020, 9030, 9040, 9050, 9060, 9070, 9080, 9090, 9100, 9110, 9120, 9131, 9132, 9133, 9140, 9150, 9160, 9170, 9180, 9190, 9200, 9210, 9220, 9230, 9240, 9250, 9260, 9270, 9280, 9290, 9300, 9310, 9320, 9330, 9350, 9340, 9360, 9370, 9380, 9390, 9400, 9410, 9420, 9430, 9440, 9450, 9461, 9462, 9471, 9472, 9473, 9474, 9910, 9920, 9931, 9932, 9933, 9934, 9935, 9936, 9941, 9942, 9943, 9951, 9952, 9960, 0];

	public void UpdateDisplay()
	{
		var zoomPoints = new List<KyoshinMonitorLib.Location>();

		switch (SelectedDCReportGroup)
		{
			case EewReportGroup e:
				{
					FeatureLayer? areaLayer = null;
					MapData?.TryGetLayer(LandLayerType.PrefectureForecastAreaForEew, out areaLayer);

					var map = new Dictionary<int, SKColor>();
					var size = new PointD(.1, .1);
					e.Reports.ForEach(r =>
					{
						for (var i = 0; i < r.WarningRegions.Length; i++)
						{
							if (r.WarningRegions[i])
							{
								map[EewAreaTable[i]] = SKColors.Tomato;
								if (areaLayer != null)
								{
									foreach (var poly in areaLayer.FindPolygon(EewAreaTable[i]))
									{
										zoomPoints.Add((poly.BoundingBox.TopLeft - size).CastLocation());
										zoomPoints.Add((poly.BoundingBox.BottomRight + size).CastLocation());
									}
								}
							}
						}
					});

					CustomColorMap = new()
					{
						{ LandLayerType.PrefectureForecastAreaForEew, map },
					};
					LayerSets = [new(0, LandLayerType.PrefectureForecastAreaForEew)];
					break;
				}
			//case MarineReportGroup m:
			//	{
			//		var map = new Dictionary<int, SKColor>();
			//		m.Reports.ForEach(r =>
			//		{
			//			foreach (var (i, c) in r.Regions)
			//			{
			//				map[c] = SKColors.Red;
			//			}
			//		});

			//		CustomColorMap = new()
			//		{
			//			{ LandLayerType.LocalMarineForecastArea, map },
			//		};
			//		break;
			//	}
			case SeismicIntensityReportGroup si:
				{
					FeatureLayer? cityLayer = null;
					MapData?.TryGetLayer(LandLayerType.EarthquakeInformationPrefecture, out cityLayer);

					var map = new Dictionary<int, SKColor>();
					var size = new PointD(.1, .1);

					foreach (var r in si.Reports)
					{
						foreach (var (i, c) in r.Regions)
						{
							var areaIntensity = i switch
							{
								SeismicIntensity.LessThanInt4 => JmaIntensity.Int3,
								SeismicIntensity.Int4 => JmaIntensity.Int4,
								SeismicIntensity.Int5Lower => JmaIntensity.Int5Lower,
								SeismicIntensity.Int5Upper => JmaIntensity.Int5Upper,
								SeismicIntensity.Int6Lower => JmaIntensity.Int6Lower,
								SeismicIntensity.Int6Upper => JmaIntensity.Int6Upper,
								SeismicIntensity.Int7 => JmaIntensity.Int7,
								_ => JmaIntensity.Unknown,
							};
							map[c] = FixedObjectRenderer.IntensityPaintCache[areaIntensity].Background.Color;

							if (cityLayer != null)
							{
								foreach (var cityPoly in cityLayer.FindPolygon(c))
								{
									zoomPoints.Add((cityPoly.BoundingBox.TopLeft - size).CastLocation());
									zoomPoints.Add((cityPoly.BoundingBox.BottomRight + size).CastLocation());
								}
							}
						}
					}

					LayerSets = [new(0, LandLayerType.EarthquakeInformationPrefecture)];
					CustomColorMap = new() {
						{ LandLayerType.EarthquakeInformationPrefecture, map },
					};
				}
				break;
			default:
				CustomColorMap = null;
				LayerSets = LandLayerSet.DefaultLayerSets;
				break;
		}

		if (zoomPoints.Count != 0)
		{
			// 自動ズーム範囲を計算
			var minLat = float.MaxValue;
			var maxLat = float.MinValue;
			var minLng = float.MaxValue;
			var maxLng = float.MinValue;
			foreach (var p in zoomPoints)
			{
				if (minLat > p.Latitude)
					minLat = p.Latitude;
				if (minLng > p.Longitude)
					minLng = p.Longitude;

				if (maxLat < p.Latitude)
					maxLat = p.Latitude;
				if (maxLng < p.Longitude)
					maxLng = p.Longitude;
			}
			var rect = new Rect(minLat, minLng, maxLat - minLat, maxLng - minLng);

			FocusBound = rect;
		}
		else
			FocusBound = null;

	}
}
