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

	/// <summary>
	/// 受信時刻を指定して受信済みの電文かを判定する<br/>
	/// 電文に日付が含まれず、同じ内容の電文が後日再び送られうるグループがオーバーライドする
	/// </summary>
	/// <param name="report">電文</param>
	/// <param name="receivedTime">受信時刻</param>
	public virtual bool CheckDuplicate(DCReport report, DateTimeOffset receivedTime) => CheckDuplicate(report);

	/// <summary>
	/// 受信時刻を指定して電文の統合を試みる<br/>
	/// 電文に発表時刻が含まれず、受信時刻で統合の可否を判断するグループがオーバーライドする
	/// </summary>
	/// <param name="report">電文</param>
	/// <param name="receivedTime">受信時刻</param>
	public virtual bool TryProcess(DCReport report, DateTimeOffset receivedTime) => TryProcess(report);

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
