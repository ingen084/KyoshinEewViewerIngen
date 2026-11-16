using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Models;

/// <summary>
/// J アラートの電文の履歴
/// </summary>
/// <param name="ReceivedTime">受信時刻 (表示用のタイムゾーン)</param>
/// <param name="MessageTypeName">A1 メッセージ種別</param>
/// <param name="Summary">避難行動指示の要約。要約がない場合は災害の種別</param>
/// <param name="TargetAreaText">対象地域</param>
public record JAlertHistoryItem(DateTime ReceivedTime, string MessageTypeName, string Summary, string TargetAreaText);

/// <summary>
/// J アラートの電文をまとめたグループ<br/>
/// ミサイル発射から通過までのように短時間に続けて発表される電文を 1 つにまとめ、指示の推移を表示する
/// </summary>
public partial class JAlertReportGroup : DCXReportGroup
{
	/// <summary>
	/// 前回の電文からこの時間以内に受信した電文を統合する<br/>
	/// J アラートは継続時間が不明で解除も送られないため、一連の事案を受信間隔で区切る
	/// </summary>
	private static readonly TimeSpan MergeWindow = TimeSpan.FromHours(1);

	private MapData? MapData { get; }
	private DateTimeOffset LastReceivedTimeUtc { get; set; }

	/// <summary>
	/// 最新の電文のメッセージ種別
	/// </summary>
	[ObservableProperty]
	public partial string MessageTypeName { get; private set; } = "";

	/// <summary>
	/// 最新の電文の深刻度
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsModerate), nameof(IsSevere), nameof(IsExtreme))]
	public partial AlertSeverity Severity { get; private set; }

	public bool IsModerate => Severity == AlertSeverity.Moderate;
	public bool IsSevere => Severity == AlertSeverity.Severe;
	public bool IsExtreme => Severity == AlertSeverity.Extreme;

	/// <summary>
	/// 最新の電文の深刻度の名称
	/// </summary>
	[ObservableProperty]
	public partial string SeverityName { get; private set; } = "";

	/// <summary>
	/// 最新の電文の避難行動指示。指示がない場合は null
	/// </summary>
	[ObservableProperty]
	public partial string? InstructionText { get; private set; }

	/// <summary>
	/// 最新の電文の要約 (一覧表示用)
	/// </summary>
	[ObservableProperty]
	public partial string Summary { get; private set; } = "";

	/// <summary>
	/// 最新の電文の発生日時 (表示用のタイムゾーン)
	/// </summary>
	[ObservableProperty]
	public partial DateTime? OnsetTime { get; private set; }

	/// <summary>
	/// 最新の電文の対象地域名
	/// </summary>
	[ObservableProperty]
	public partial string[] TargetAreaNames { get; private set; } = [];

	/// <summary>
	/// 最新の電文の対象地域の数 (全都道府県の場合は 47)
	/// </summary>
	[ObservableProperty]
	public partial int TargetAreaCount { get; private set; }

	/// <summary>
	/// 受信した電文の履歴 (新しい順)
	/// </summary>
	public ObservableCollection<JAlertHistoryItem> History { get; } = [];

	public JAlertReportGroup(JAlertDCXReport report, DateTimeOffset receivedTime, MapData? mapData) : base(report, receivedTime)
	{
		MapData = mapData;
		Process(report, receivedTime);
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new JAlertReportControl { DataContext = this };

	protected override bool CanMerge(DCXReport report, DateTimeOffset receivedTime)
		=> receivedTime - LastReceivedTimeUtc <= MergeWindow;

	protected override void Process(DCXReport report, DateTimeOffset receivedTime)
	{
		if (report is not JAlertDCXReport jAlert)
			return;
		var camf = jAlert.CommonAlertMessage;
		LastReceivedTimeUtc = receivedTime;

		Severity = camf.Severity;
		SeverityName = DCXDictionary.GetSeverityName(camf.Severity);
		// J アラートの解除は誤報の場合にのみ送られる (仕様書 5.4.2)。誤報の指示を表示しないよう指示文は扱わない
		if (camf.MessageType == AlertMessageType.AllClear)
		{
			MessageTypeName = "取消";
			InstructionText = null;
			Summary = "誤報による取消";
		}
		else
		{
			MessageTypeName = DCXDictionary.GetMessageTypeName(camf.MessageType);
			InstructionText = DCXDictionary.GetInstructionText(jAlert);
			Summary = DCXDictionary.GetJAlertInstructionSummary(camf.InstructionCode) ?? HazardName;
		}
		OnsetTime = camf.GetHazardOnsetTime(receivedTime) is { } onset ? ApplyTimezoneOffset(onset) : null;

		if (jAlert.TargetAreaCodeListType == TargetAreaCodeListType.Prefecture)
		{
			TargetAreaNames = jAlert.PrefectureMask == JAlertDCXReport.AllPrefecturesMask
				? ["全都道府県"]
				: [.. jAlert.PrefectureCodes.Select(DCXDictionary.GetPrefectureName)];
			TargetAreaCount = jAlert.PrefectureCodes.Count();
		}
		else
		{
			TargetAreaNames = [.. jAlert.MunicipalityCodes.Select(DCXDictionary.GetMunicipalityName)];
			TargetAreaCount = jAlert.MunicipalityCodes.Length;
		}

		History.Insert(0, new(ApplyTimezoneOffset(receivedTime), MessageTypeName, Summary, string.Join("、", TargetAreaNames)));
		UpdateMap(jAlert);
	}

	private void UpdateMap(JAlertDCXReport report)
	{
		var (layerType, codes) = report.TargetAreaCodeListType == TargetAreaCodeListType.Prefecture
			? (LandLayerType.EarthquakeInformationPrefecture, report.PrefectureCodes.Select(c => (long)c).ToArray())
			: (LandLayerType.MunicipalityEarthquakeTsunamiArea, report.MunicipalityCodes.SelectMany(DCXDictionary.GetMunicipalityAreaCodes).ToArray());

		// 誤報の取消 (解除) の場合は着色しない
		var colorMap = new Dictionary<long, SKColor>();
		if (report.CommonAlertMessage.MessageType != AlertMessageType.AllClear)
		{
			var color = GetSeverityColor(report.CommonAlertMessage.Severity);
			foreach (var code in codes)
				colorMap[code] = color;
		}

		var zoomPoints = new List<Location>();
		FeatureLayer? layer = null;
		MapData?.TryGetLayer(layerType, out layer);
		if (layer != null)
			foreach (var code in codes)
				foreach (var polygon in layer.FindPolygon(code))
				{
					zoomPoints.Add(polygon.BoundingBox.TopLeft.CastLocation());
					zoomPoints.Add(polygon.BoundingBox.BottomRight.CastLocation());
				}

		MapDisplayParameter = new()
		{
			// 左側に表示する詳細と地図が重ならないようにする
			Padding = new(355, 0, 0, 0),
			CustomColorMap = colorMap.Count > 0 ? new() { { layerType, colorMap } } : null,
		};
		MapNavigationRequest = CreateNavigationRequest(zoomPoints);
	}
}
