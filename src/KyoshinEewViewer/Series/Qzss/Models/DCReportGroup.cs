using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public abstract partial class DCReportGroup : DisasterCrisisInformation
{
	/// <summary>
	/// タイムゾーンオフセット(時間)
	/// </summary>
	public static int TimezoneOffset { get; set; } = -9;

	/// <summary>
	/// DateTimeOffsetをタイムゾーンオフセットを適用したDateTimeに変換する
	/// </summary>
	protected static DateTime ApplyTimezoneOffset(DateTimeOffset dateTimeOffset)
		=> dateTimeOffset.UtcDateTime.AddHours(-TimezoneOffset);

	public bool IsTestOrDrill => Classification == ReportClassification.TrainingOrTest;

	[ObservableProperty]
	public partial ReportClassification Classification { get; set; }

	[ObservableProperty]
	public partial InformationType? InformationType { get; set; }

	[ObservableProperty]
	public partial int ReportCount { get; set; } = 1;

	[ObservableProperty]
	public partial DateTime ReportTime { get; set; }

	public abstract bool CheckDuplicate(DCReport report);
	public abstract bool TryProcess(DCReport report);

	[JsonIgnore]
	public abstract Control? DetailDisplayControl { get; }

	/// <summary>
	/// マップ表示位置のリクエスト
	/// </summary>
	[JsonIgnore]
	[ObservableProperty]
	public partial MapNavigationRequest? MapNavigationRequest { get; protected set; }

	/// <summary>
	/// マップ表示用のパラメータ
	/// </summary>
	[JsonIgnore]
	[ObservableProperty]
	public partial MapDisplayParameter MapDisplayParameter { get; protected set; }
}
