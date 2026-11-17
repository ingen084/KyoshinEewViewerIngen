using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.Map;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Models;

/// <summary>
/// 災危通報(拡張) の電文をまとめたグループ
/// </summary>
public abstract partial class DCXReportGroup : DCReportGroup
{
	public static readonly string TYPE = "DCX";
	public override string Type => TYPE;

	/// <summary>
	/// 同じ内容の電文を重複とみなす期間<br/>
	/// DCX の電文には日付が含まれず、同じ内容の電文が後日 (定期的な試験用データの配信など) 再び送られうるため期間を限る
	/// </summary>
	private static readonly TimeSpan DuplicateWindow = TimeSpan.FromDays(1);

	private List<(DCXReport Report, DateTimeOffset ReceivedTime)> Reports { get; } = [];

	protected DCXReportGroup(DCXReport report, DateTimeOffset receivedTime)
	{
		var camf = report.CommonAlertMessage;
		KindName = DCXDictionary.GetKindName(report);
		KindShortName = DCXDictionary.GetKindShortName(report);
		CountryRegionCode = camf.CountryRegionCode;
		CountryRegionName = DCXDictionary.GetCountryRegionName(camf.CountryRegionCode);
		ProviderIdentifier = camf.ProviderIdentifier;
		ProviderName = DCXDictionary.GetProviderName(camf.CountryRegionCode, camf.ProviderIdentifier);
		HazardCode = camf.HazardCategoryAndType;
		HazardName = DCXDictionary.GetHazardName(camf.HazardCategoryAndType);
		HazardCategoryName = DCXDictionary.GetHazardCategoryName(camf.HazardCategoryAndType);

		Classification = report.IsTest ? ReportClassification.TrainingOrTest : ReportClassification.Regular;
		// DCX の電文には発表時刻がないため、最初に受信した時刻を表示に使用する
		ReportTime = ApplyTimezoneOffset(receivedTime);
		LastReceivedTime = ReportTime;
		ReportKind = report.GetType();

		Reports.Add((report, receivedTime));
	}

	/// <summary>
	/// 情報の種別名 (Lアラート、Jアラートなど)
	/// </summary>
	public string KindName { get; }

	/// <summary>
	/// 情報の種別名 (一覧表示用の短い名前)
	/// </summary>
	public string KindShortName { get; }

	/// <summary>
	/// A2 国・地域名コード
	/// </summary>
	public ushort CountryRegionCode { get; }

	/// <summary>
	/// A2 国・地域名
	/// </summary>
	public string CountryRegionName { get; }

	/// <summary>
	/// A3 提供機関の識別子
	/// </summary>
	public byte ProviderIdentifier { get; }

	/// <summary>
	/// A3 提供機関名
	/// </summary>
	public string ProviderName { get; }

	/// <summary>
	/// A4 災害のカテゴリと種別
	/// </summary>
	public byte HazardCode { get; }

	/// <summary>
	/// A4 災害の種別名
	/// </summary>
	public string HazardName { get; }

	/// <summary>
	/// A4 災害のカテゴリ名
	/// </summary>
	public string HazardCategoryName { get; }

	/// <summary>
	/// 最後に電文を受信した時刻
	/// </summary>
	[ObservableProperty]
	public partial DateTime LastReceivedTime { get; protected set; }

	private Type ReportKind { get; }

	public override bool CheckDuplicate(DCReport report)
		=> CheckDuplicate(report, DateTimeOffset.UtcNow);

	public override bool CheckDuplicate(DCReport report, DateTimeOffset receivedTime)
		=> report is DCXReport d && Reports.Any(r => receivedTime - r.ReceivedTime < DuplicateWindow && r.Report.HasSameContent(d));

	public override bool TryProcess(DCReport report)
		=> TryProcess(report, DateTimeOffset.UtcNow);

	public override bool TryProcess(DCReport report, DateTimeOffset receivedTime)
	{
		if (report is not DCXReport d || !IsSameAlert(d) || !CanMerge(d, receivedTime))
			return false;

		Reports.Add((d, receivedTime));
		ReportCount++;
		LastReceivedTime = ApplyTimezoneOffset(receivedTime);
		Process(d, receivedTime);
		return true;
	}

	/// <summary>
	/// 同じ災害の電文かどうか (種別、国・地域、提供機関、災害種別、試験かどうかが一致する)
	/// </summary>
	private bool IsSameAlert(DCXReport report)
	{
		var camf = report.CommonAlertMessage;
		return report.GetType() == ReportKind
			&& camf.CountryRegionCode == CountryRegionCode
			&& camf.ProviderIdentifier == ProviderIdentifier
			&& camf.HazardCategoryAndType == HazardCode
			&& report.IsTest == IsTestOrDrill;
	}

	/// <summary>
	/// 同じ災害の電文をこのグループに統合してよいか
	/// </summary>
	protected abstract bool CanMerge(DCXReport report, DateTimeOffset receivedTime);

	/// <summary>
	/// グループに追加された電文を反映する
	/// </summary>
	protected abstract void Process(DCXReport report, DateTimeOffset receivedTime);

	/// <summary>
	/// 深刻度に応じた地図の塗りつぶしの色 (警戒レベルの色に対応させる)
	/// </summary>
	protected static SKColor GetSeverityColor(AlertSeverity severity)
	{
		var key = severity switch
		{
			AlertSeverity.Moderate => "WeatherWarningLevel3Color",
			AlertSeverity.Severe => "WeatherWarningLevel4Color",
			AlertSeverity.Extreme => "WeatherWarningLevel5Color",
			_ => "WeatherWarningLevel2Color",
		};
		if (KyoshinEewViewerApp.Application?.FindResource(key) is not Color color)
			return SKColors.Gray;
		return new(color.R, color.G, color.B, color.A);
	}

	/// <summary>
	/// 指定した地点がすべて入る範囲を表示する地図の移動要求を作成する
	/// </summary>
	/// <returns>移動要求。地点がない場合は null</returns>
	protected static MapNavigationRequest? CreateNavigationRequest(IReadOnlyCollection<Location> points)
	{
		if (points.Count == 0)
			return null;

		// 区域や楕円の端が画面の端に接しないよう余白をとる
		const float margin = 0.1f;
		return new(new Location[]
		{
			new(points.Min(p => p.Latitude) - margin, points.Min(p => p.Longitude) - margin),
			new(points.Max(p => p.Latitude) + margin, points.Max(p => p.Longitude) + margin),
		}.CalcRect());
	}
}
