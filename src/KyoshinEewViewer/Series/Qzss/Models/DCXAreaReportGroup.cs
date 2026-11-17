using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.Qzss.Layers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Models;

/// <summary>
/// 地域ごとの最新の状態
/// </summary>
/// <param name="AreaCode">EX1 対象市区町村コード (国外機関の情報は 0)</param>
/// <param name="AreaName">地域名</param>
/// <param name="MessageType">A1 メッセージ種別</param>
/// <param name="Severity">A5 深刻度</param>
/// <param name="InstructionText">A11 避難行動指示。指示がない場合は null</param>
/// <param name="AdditionalInstructionText">EX2 追加対象地域への指示。指示がない場合は null</param>
/// <param name="OnsetTime">A6/A7 発生日時 (表示用のタイムゾーン)</param>
/// <param name="Duration">A8 継続時間</param>
/// <param name="ExpiresAt">有効期限</param>
/// <param name="ReceivedTime">受信時刻 (表示用のタイムゾーン)</param>
/// <param name="Ellipse">A12〜A16 対象地域の楕円</param>
/// <param name="AdditionalTargetArea">EX2〜EX7 追加対象地域</param>
public record DCXAreaState(
	ushort AreaCode,
	string AreaName,
	AlertMessageType MessageType,
	AlertSeverity Severity,
	string? InstructionText,
	string? AdditionalInstructionText,
	DateTime? OnsetTime,
	HazardDuration Duration,
	DateTimeOffset ExpiresAt,
	DateTime ReceivedTime,
	DcxEllipse? Ellipse,
	AdditionalTargetArea? AdditionalTargetArea
)
{
	public string MessageTypeName => DCXDictionary.GetMessageTypeName(MessageType);
	public string SeverityName => DCXDictionary.GetSeverityName(Severity);
	public string DurationName => DCXDictionary.GetHazardDurationName(Duration);
	public bool IsCleared => MessageType == AlertMessageType.AllClear;
	public bool IsModerate => Severity == AlertSeverity.Moderate;
	public bool IsSevere => Severity == AlertSeverity.Severe;
	public bool IsExtreme => Severity == AlertSeverity.Extreme;
}

/// <summary>
/// 地域ごとに状態を持つ災危通報(拡張) のグループ (L アラート、自治体発信情報、国外機関の情報)<br/>
/// 同じ災害の電文を 1 つにまとめ、更新・解除は該当する地域の状態に反映する
/// </summary>
public partial class DCXAreaReportGroup : DCXReportGroup
{
	/// <summary>
	/// 継続時間が不明な場合に有効とみなす期間 (仕様書 5.4.1)
	/// </summary>
	private static readonly TimeSpan UnknownDurationExpiry = TimeSpan.FromDays(7);

	private MapData? MapData { get; }
	private DCXEllipseLayer Layer { get; } = new();

	/// <summary>
	/// 地域ごとの最新の状態
	/// </summary>
	public ObservableCollection<DCXAreaState> Areas { get; } = [];

	/// <summary>
	/// 地域の数
	/// </summary>
	[ObservableProperty]
	public partial int AreaCount { get; private set; }

	/// <summary>
	/// 解除されていない地域のうち最も高い深刻度
	/// </summary>
	[ObservableProperty]
	public partial AlertSeverity MaxSeverity { get; private set; }

	/// <summary>
	/// 最も高い深刻度の名称
	/// </summary>
	[ObservableProperty]
	public partial string MaxSeverityName { get; private set; } = "";

	/// <summary>
	/// 一覧に表示するグループの状態 (試験・解除・発表中)
	/// </summary>
	[ObservableProperty]
	public partial string StatusName { get; private set; } = "";

	/// <summary>
	/// すべての地域が解除されたか
	/// </summary>
	[ObservableProperty]
	public partial bool IsAllCleared { get; private set; }

	public DCXAreaReportGroup(DCXReport report, DateTimeOffset receivedTime, MapData? mapData) : base(report, receivedTime)
	{
		MapData = mapData;
		Process(report, receivedTime);
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new DCXAreaReportControl { DataContext = this };

	/// <summary>
	/// 解除されておらず有効期限内の地域が残っている間だけ統合する
	/// </summary>
	protected override bool CanMerge(DCXReport report, DateTimeOffset receivedTime)
		=> Areas.Any(a => !a.IsCleared && a.ExpiresAt > receivedTime);

	protected override void Process(DCXReport report, DateTimeOffset receivedTime)
	{
		var state = CreateState(report, receivedTime);

		if (Areas.FirstOrDefault(a => a.AreaCode == state.AreaCode) is { } existing)
			Areas[Areas.IndexOf(existing)] = state;
		else
			// 地域コード順に並べる
			Areas.Insert(Areas.TakeWhile(a => a.AreaCode < state.AreaCode).Count(), state);

		UpdateSummary();
		UpdateMap();
	}

	private DCXAreaState CreateState(DCXReport report, DateTimeOffset receivedTime)
	{
		var camf = report.CommonAlertMessage;
		var onset = camf.GetHazardOnsetTime(receivedTime);

		// 継続時間が分かる場合は発生時刻 (不明なら受信時刻) から、不明な場合は受信から一定期間を有効とする
		var expiresAt = camf.HazardDurationUpperBound is { } duration
			? (onset ?? receivedTime) + duration
			: receivedTime + UnknownDurationExpiry;

		var (areaCode, areaName) = report switch
		{
			EvacuationDCXReport { TargetAreaCode: 0 } => ((ushort)0, "市区町村の指定なし"),
			EvacuationDCXReport e => (e.TargetAreaCode, DCXDictionary.GetMunicipalityName(e.TargetAreaCode)),
			_ => ((ushort)0, CountryRegionName),
		};
		var additionalTargetArea = (report as LocalGovernmentDCXReport)?.AdditionalTargetArea;
		var instructionText = DCXDictionary.GetInstructionText(report);
		var additionalInstructionText = additionalTargetArea is { } a ? DCXDictionary.GetEvacuateDirectionText(a.Direction) : null;
		// 自治体発信情報で A11 が指定なしの場合は、追加対象地域への指示が避難行動の指示になる
		if (instructionText == null)
			(instructionText, additionalInstructionText) = (additionalInstructionText, null);

		return new DCXAreaState(
			areaCode,
			areaName,
			camf.MessageType,
			camf.Severity,
			instructionText,
			additionalInstructionText,
			onset is { } o ? ApplyTimezoneOffset(o) : null,
			camf.HazardDuration,
			expiresAt,
			ApplyTimezoneOffset(receivedTime),
			camf.Ellipse,
			additionalTargetArea
		);
	}

	private void UpdateSummary()
	{
		AreaCount = Areas.Count;
		IsAllCleared = Areas.All(a => a.IsCleared);
		StatusName = IsTestOrDrill ? "試験" : IsAllCleared ? "解除" : "発表中";
		var targetAreas = IsAllCleared ? Areas : Areas.Where(a => !a.IsCleared);
		MaxSeverity = targetAreas.Select(a => a.Severity).DefaultIfEmpty(AlertSeverity.Unknown).Max();
		MaxSeverityName = DCXDictionary.GetSeverityName(MaxSeverity);
	}

	private void UpdateMap()
	{
		var ellipses = new List<DCXEllipseItem>();
		var colorMap = new Dictionary<long, SKColor>();
		var zoomPoints = new List<Location>();

		FeatureLayer? cityLayer = null;
		MapData?.TryGetLayer(LandLayerType.MunicipalityEarthquakeTsunamiArea, out cityLayer);

		foreach (var area in Areas)
		{
			// 解除された地域は着色せず、表示範囲もすべての地域が解除された場合のみ含める
			if (area.IsCleared && !IsAllCleared)
				continue;

			if (!area.IsCleared)
			{
				if (area.Ellipse is { } ellipse)
					ellipses.Add(new(ellipse, area.Severity));
				if (area.AdditionalTargetArea is { } additional)
					ellipses.Add(new(additional.Ellipse, area.Severity, additional.Direction));
			}

			foreach (var ellipse in new[] { area.Ellipse, area.AdditionalTargetArea?.Ellipse })
				if (ellipse is { } e)
					zoomPoints.AddRange(e.ToPolygon(8).Select(p => new Location((float)p.Latitude, (float)p.Longitude)));

			// 楕円がある場合は楕円が対象地域となり、市区町村は楕円がない場合のみ参照する (仕様書 5.8.1)
			if (area.AreaCode == 0 || area.Ellipse != null)
				continue;
			foreach (var code in DCXDictionary.GetMunicipalityAreaCodes(area.AreaCode))
			{
				if (!area.IsCleared)
					colorMap[code] = GetSeverityColor(area.Severity);
				if (cityLayer == null)
					continue;
				foreach (var polygon in cityLayer.FindPolygon(code))
				{
					zoomPoints.Add(polygon.BoundingBox.TopLeft.CastLocation());
					zoomPoints.Add(polygon.BoundingBox.BottomRight.CastLocation());
				}
			}
		}

		Layer.Ellipses = [.. ellipses];
		MapDisplayParameter = new()
		{
			// 左側に表示する詳細と地図が重ならないようにする
			Padding = new(355, 0, 0, 0),
			OverlayLayers = [Layer],
			CustomColorMap = colorMap.Count > 0 ? new() { { LandLayerType.MunicipalityEarthquakeTsunamiArea, colorMap } } : null,
		};

		MapNavigationRequest = CreateNavigationRequest(zoomPoints);
	}
}
