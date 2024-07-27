using KyoshinMonitorLib;
using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Tsunami.Models;
public class TsunamiInfo
{
	/// <summary>
	/// イベントID
	/// </summary>
	public string? EventId { get; set; }

	/// <summary>
	/// 通常ではない電文の状態(訓練/試験)
	/// </summary>
	public string? SpecialState { get; set; }

	/// <summary>
	/// 発表時刻
	/// </summary>
	public DateTime ReportedAt { get; set; }

	/// <summary>
	/// 有効期限
	/// </summary>
	public DateTime? ExpireAt { get; set; }

	/// <summary>
	/// 津波情報がないが観測情報が存在する地域(例外処理)
	/// </summary>
	public TsunamiWarningArea[]? NoTsunamiAreas { get; set; }

	/// <summary>
	/// 津波予報
	/// </summary>
	public TsunamiWarningArea[]? ForecastAreas { get; set; }

	/// <summary>
	/// 津波注意報
	/// </summary>
	public TsunamiWarningArea[]? AdvisoryAreas { get; set; }

	/// <summary>
	/// 津波警報
	/// </summary>
	public TsunamiWarningArea[]? WarningAreas { get; set; }

	/// <summary>
	/// 大津波警報
	/// </summary>
	public TsunamiWarningArea[]? MajorWarningAreas { get; set; }

	public TsunamiLevel Level =>
		MajorWarningAreas != null ? TsunamiLevel.MajorWarning : WarningAreas != null ? TsunamiLevel.Warning : AdvisoryAreas != null ? TsunamiLevel.Advisory : ForecastAreas != null ? TsunamiLevel.Forecast : TsunamiLevel.None;

	public bool CheckExpired(DateTime time)
	{
		if (ExpireAt != null)
			return ExpireAt < time;
		if (Level == TsunamiLevel.None || Level == TsunamiLevel.Forecast)
			return ReportedAt.AddDays(1) < time;
		return false;
	}
}

public enum TsunamiLevel
{
	None,
	Forecast,
	Advisory,
	Warning,
	MajorWarning,
}

/// <summary>
/// 津波警報の情報
/// </summary>
public record TsunamiWarningArea(int Code, string Name, string Height, string State)
{
	[JsonIgnore]
	public required DateTime ArrivalTime { get; init; }
	public TsunamiObservationStation[]? Stations { get; set; }
}

/// <summary>
/// 津波観測点
/// </summary>
public record TsunamiObservationStation(int Code, string Name, string? NameKana, Location? Location)
{
	[JsonIgnore]
	public required DateTime ArrivalTime { get; init; }

	public DateTimeOffset? HighTideTime { get; set; } = null;
	public string FirstHeight { get; set; } = "";
	public string FirstHeightDetail { get; set; } = "";

	public DateTimeOffset? MaxHeightTime { get; set; } = null;
	public float? MaxHeight { get; set; } = null;
	public string MaxHeightDetail { get; set; } = "-";

	public bool IsRising { get; set; } = false;
	public bool IsOutRange { get; set; } = false;
}
