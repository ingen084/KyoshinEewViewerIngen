using System;

namespace KyoshinEewViewer.Series.Tsunami.Models;
public class TsunamiInfo
{
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
public record TsunamiWarningArea(int Code, string Name, string Height, string State, DateTime ArrivalTime);
