using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;

namespace KyoshinEewViewer.Series.Qzss.Models;

public abstract class DCReportGroup : ReactiveObject
{
	private ReportClassification _classification;
	public ReportClassification Classification
	{
		get => _classification;
		set => this.RaiseAndSetIfChanged(ref _classification, value);
	}

	private InformationType? _informationType;
	public InformationType? InformationType
	{
		get => _informationType;
		set => this.RaiseAndSetIfChanged(ref _informationType, value);
	}

	public abstract bool CheckDuplicate(DCReport report);
	public abstract bool TryProcess(DCReport report);
}

public class EewReportGroup : DCReportGroup
{
	public List<EewReport> Reports { get; } = [];

	private DateTime _occurrenceTime;
	public DateTime OccurrenceTime
	{
		get => _occurrenceTime;
		set => this.RaiseAndSetIfChanged(ref _occurrenceTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private EewSeismicIntensity _intensity;
	public EewSeismicIntensity Intensity
	{
		get => _intensity;
		set => this.RaiseAndSetIfChanged(ref _intensity, value);
	}

	private bool _isIntensityOver;
	public bool IsIntensityOver
	{
		get => _isIntensityOver;
		set => this.RaiseAndSetIfChanged(ref _isIntensityOver, value);
	}

	private byte _rawMagnitude;
	public byte RawMagnitude
	{
		get => _rawMagnitude;
		set => this.RaiseAndSetIfChanged(ref _rawMagnitude, value);
	}

	private int _epicenter;
	public int Epicenter
	{
		get => _epicenter;
		set => this.RaiseAndSetIfChanged(ref _epicenter, value);
	}

	public EewReportGroup(EewReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		OccurrenceTime = report.OccurrenceTime.LocalDateTime;
		TotalAreaCount = report.WarningRegions.Count(r => r);
		Intensity = report.SeismicIntensityLowerLimit;
		IsIntensityOver = report.SeismicIntensityUpperLimit == EewSeismicIntensity.Over;
		RawMagnitude = report.Magnitude;
		Epicenter = report.Epicenter;

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is EewReport eew && Reports.Any(r => eew.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report) => false;
}

public class SeismicIntensityReportGroup : DCReportGroup
{
	public List<SeismicIntensityReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private SeismicIntensity _maxIntensity;
	public SeismicIntensity MaxIntensity
	{
		get => _maxIntensity;
		set => this.RaiseAndSetIfChanged(ref _maxIntensity, value);
	}

	public SeismicIntensityReportGroup(SeismicIntensityReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		MaxIntensity = report.Regions.Max(r => r.Intensity);

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is SeismicIntensityReport si && Reports.Any(r => si.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not SeismicIntensityReport si || si.ReportTime.LocalDateTime != ReportTime)
			return false;

		Reports.Add(si);
		TotalAreaCount += si.Regions.Count(a => a.Region != 0);
		var max = si.Regions.Max(r => r.Intensity);
		if (max > MaxIntensity)
			MaxIntensity = max;
		return true;
	}
}

public class HypocenterReportGroup : DCReportGroup
{
	public List<HypocenterReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private DateTime _occurrenceTime;
	public DateTime OccurrenceTime
	{
		get => _occurrenceTime;
		set => this.RaiseAndSetIfChanged(ref _occurrenceTime, value);
	}

	private byte _rawMagnitude;
	public byte RawMagnitude
	{
		get => _rawMagnitude;
		set => this.RaiseAndSetIfChanged(ref _rawMagnitude, value);
	}

	private int _epicenter;
	public int Epicenter
	{
		get => _epicenter;
		set => this.RaiseAndSetIfChanged(ref _epicenter, value);
	}

	public HypocenterReportGroup(HypocenterReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		OccurrenceTime = report.OccurrenceTime.LocalDateTime;
		RawMagnitude = report.Magnitude;
		Epicenter = report.Epicenter;

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is HypocenterReport h && Reports.Any(r => h.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report) => false;
}

public class NankaiTroughEarthquakeReportGroup : DCReportGroup
{
	public List<NankaiTroughEarthquakeReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private byte _totalPage;
	public byte TotalPage
	{
		get => _totalPage;
		set => this.RaiseAndSetIfChanged(ref _totalPage, value);
	}

	private byte _currentProgress;
	public byte CurrentProgress
	{
		get => _currentProgress;
		set => this.RaiseAndSetIfChanged(ref _currentProgress, value);
	}

	private readonly ObservableAsPropertyHelper<string?> _currentProgressString;
	public string? CurrentProgressString => _currentProgressString?.Value;

	private InformationSerialCode _informationSerialCode;
	public InformationSerialCode InformationSerialCode
	{
		get => _informationSerialCode;
		set => this.RaiseAndSetIfChanged(ref _informationSerialCode, value);
	}

	private string? _contents;
	public string? Contents
	{
		get => _contents;
		set => this.RaiseAndSetIfChanged(ref _contents, value);
	}

	public NankaiTroughEarthquakeReportGroup(NankaiTroughEarthquakeReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		_currentProgressString = this.WhenAnyValue(x => x.CurrentProgress, x => x.TotalPage)
			.Select(x => x.Item1 == x.Item2 ? "受信完了" : $"{x.Item1}/{x.Item2}").ToProperty(this, x => x.CurrentProgressString);

		ReportTime = report.ReportTime.LocalDateTime;
		TotalPage = report.TotalPage;
		InformationSerialCode = report.InformationSerialCode;

		CurrentProgress = 1;
		Reports.Add(report);
		GenerateContents();
	}

	public override bool CheckDuplicate(DCReport report) => report is NankaiTroughEarthquakeReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not NankaiTroughEarthquakeReport n)
			return false;

		if (n.InformationSerialCode != InformationSerialCode || n.TotalPage != TotalPage || Reports.Any(r => n.PageNumber == r.PageNumber))
			return false;

		CurrentProgress++;
		Reports.Add(n);
		GenerateContents();
		return true;
	}

	private void GenerateContents()
		=> Contents = Encoding.UTF8.GetString(Reports.OrderBy(x => x.PageNumber).SelectMany(x => x.TextInformation).ToArray());
}

public class TsunamiReportGroup : DCReportGroup
{
	public List<TsunamiReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private byte _warningCode;
	public byte WarningCode
	{
		get => _warningCode;
		set => this.RaiseAndSetIfChanged(ref _warningCode, value);
	}

	public TsunamiReportGroup(TsunamiReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		WarningCode = report.WarningCode;

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is TsunamiReport t && Reports.Any(r => t.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not TsunamiReport t || t.ReportTime.LocalDateTime != ReportTime || t.WarningCode != WarningCode)
			return false;

		Reports.Add(t);
		TotalAreaCount += t.Regions.Count(a => a.Region != 0);
		return true;
	}
}

public class NorthwestPacificTsunamiReportGroup : DCReportGroup
{
	public List<NorthwestPacificTsunamiReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private byte _tsunamigenicPotential;
	public byte TsunamigenicPotential
	{
		get => _tsunamigenicPotential;
		set => this.RaiseAndSetIfChanged(ref _tsunamigenicPotential, value);
	}

	public NorthwestPacificTsunamiReportGroup(NorthwestPacificTsunamiReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		TsunamigenicPotential = report.TsunamigenicPotential;
	}

	public override bool CheckDuplicate(DCReport report) => report is NorthwestPacificTsunamiReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not NorthwestPacificTsunamiReport n || n.ReportTime.LocalDateTime != ReportTime || n.TsunamigenicPotential != TsunamigenicPotential)
			return false;

		Reports.Add(n);
		TotalAreaCount += n.Regions.Count(a => a.Region != 0);
		return true;
	}
}

public class VolcanoReportGroup : DCReportGroup
{
	public List<VolcanoReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private int _volcanoNameCode;
	public int VolcanoNameCode
	{
		get => _volcanoNameCode;
		set => this.RaiseAndSetIfChanged(ref _volcanoNameCode, value);
	}

	private byte _warningCode;
	public byte WarningCode
	{
		get => _warningCode;
		set => this.RaiseAndSetIfChanged(ref _warningCode, value);
	}

	public VolcanoReportGroup(VolcanoReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a != 0);
		VolcanoNameCode = report.VolcanoNameCode;
		WarningCode = report.WarningCode;

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is VolcanoReport v && Reports.Any(r => v.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not VolcanoReport v || v.ReportTime.LocalDateTime != ReportTime || v.VolcanoNameCode != VolcanoNameCode || v.WarningCode != WarningCode)
			return false;

		Reports.Add(v);
		TotalAreaCount += v.Regions.Count(a => a != 0);
		return true;
	}
}

public class AshFallReportGroup : DCReportGroup
{
	public List<AshFallReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private int _volcanoNameCode;
	public int VolcanoNameCode
	{
		get => _volcanoNameCode;
		set => this.RaiseAndSetIfChanged(ref _volcanoNameCode, value);
	}

	public AshFallReportGroup(AshFallReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		VolcanoNameCode = report.VolcanoNameCode;

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is AshFallReport a && Reports.Any(r => a.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not AshFallReport a || a.ReportTime.LocalDateTime != ReportTime || a.VolcanoNameCode != VolcanoNameCode)
			return false;

		Reports.Add(a);
		TotalAreaCount += a.Regions.Count(a => a.Region != 0);
		return true;
	}
}

public class WeatherReportGroup : DCReportGroup
{
	public List<WeatherReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private byte _warningState;
	public byte WarningState
	{
		get => _warningState;
		set => this.RaiseAndSetIfChanged(ref _warningState, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	public WeatherReportGroup(WeatherReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		WarningState = report.WarningState;
		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is WeatherReport w && Reports.Any(r => w.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not WeatherReport w || w.ReportTime.LocalDateTime != ReportTime || w.WarningState != WarningState)
			return false;

		Reports.Add(w);
		TotalAreaCount += w.Regions.Count(a => a.Region != 0);
		return true;
	}
}

public class FloodReportGroup : DCReportGroup
{
	public List<FloodReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	public FloodReportGroup(FloodReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is FloodReport f && Reports.Any(r => f.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not FloodReport f || f.ReportTime.LocalDateTime != ReportTime)
			return false;

		Reports.Add(f);
		TotalAreaCount += f.Regions.Count(a => a.Region != 0);
		return true;
	}
}

public class TyphoonReportGroup : DCReportGroup
{
	public List<TyphoonReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private byte _typhoonNumber;
	public byte TyphoonNumber
	{
		get => _typhoonNumber;
		set => this.RaiseAndSetIfChanged(ref _typhoonNumber, value);
	}

	public TyphoonReportGroup(TyphoonReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TyphoonNumber = report.TyphoonNumber;

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is TyphoonReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report) => false;
}

public class MarineReportGroup : DCReportGroup
{
	public List<MarineReport> Reports { get; } = [];
	
	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	public MarineReportGroup(MarineReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is MarineReport m && Reports.Any(r => m.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not MarineReport m || m.ReportTime.LocalDateTime != ReportTime)
			return false;

		Reports.Add(m);
		TotalAreaCount += m.Regions.Count(a => a.Region != 0);
		return true;
	}
}

public class OtherOrganizationReportGroup : DCReportGroup
{
	public List<OtherOrganizationDCReport> Reports { get; } = [];

	private string? _organizationName;
	public string? OrganizationName
	{
		get => _organizationName;
		set => this.RaiseAndSetIfChanged(ref _organizationName, value);
	}

	public OtherOrganizationReportGroup(OtherOrganizationDCReport report)
	{
		Classification = report.ReportClassification;

		Reports.Add(report);
		OrganizationName = $"{report.OrganizationCode}: " + (report.OrganizationCode switch
		{
			1 => "内閣官房",
			2 => "内閣府(防災)",
			3 => "内閣府(宇宙)",
			4 => "警察庁",
			5 => "金融庁",
			6 => "消費者庁",
			7 => "総務省",
			8 => "消防庁",
			9 => "法務省",
			10 => "公安調査庁",
			11 => "外務省",
			12 => "財務省",
			13 => "国税庁",
			14 => "文部科学省",
			15 => "文化庁",
			16 => "厚生労働省",
			17 => "農林水産省",
			18 => "林野庁",
			19 => "水産庁",
			20 => "経済産業省",
			21 => "資源エネルギー庁",
			22 => "中小企業庁",
			23 => "国土交通省(防災)",
			24 => "国土交通省(危機管理)",
			25 => "国土地理院",
			26 => "観光庁",
			27 => "海上保安庁",
			28 => "環境省",
			29 => "原子力規制委員会",
			30 => "防衛省",
			>= 45 and <= 49 => "予約済み(企業等)",
			51 => "都道府県",
			52 => "市区町村",
			53 => "公的法人",
			60 => "外国",
			_ => "不明"
		});
	}

	public override bool CheckDuplicate(DCReport report) => Reports.Any(r => report.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report) => false;
}

public class UnknownReportGroup : DCReportGroup
{
	public List<DCReport> Reports { get; } = [];

	public UnknownReportGroup(DCReport report)
	{
		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => Reports.Any(r => report.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report) => false;
}
