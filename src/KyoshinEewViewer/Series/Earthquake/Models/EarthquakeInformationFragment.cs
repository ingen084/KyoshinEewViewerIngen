using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public abstract class EarthquakeInformationFragment : ReactiveObject
{
	public static (string EventId, EarthquakeInformationFragment Fragment)[] CreateFromTsunamiJmxXmlDocument(Telegram telegram, JmaXmlDocument report)
	{
		if (report.Control.Title != "津波警報・注意報・予報a")
			throw new EarthquakeInformationFragmentProcessException($"不明な電文タイトルです: {report.Control.Title}");

		// イベントIDごとに分割する
		var eventIds = report.Head.EventId.Split(' ');
		var earthquakes = report.TsunamiBody.Earthquakes.ToArray();
		if (earthquakes.Length != eventIds.Length)
			throw new EarthquakeInformationFragmentProcessException($"eventId の数と earthquake タグの数が一致しません。 eventId: {eventIds.Length} earthquake: {report.TsunamiBody.Earthquakes.Count()}");

		var result = new (string EventId, EarthquakeInformationFragment Fragment)[eventIds.Length];

		for (var i = 0; i < eventIds.Length; i++)
		{
			var earthquake = earthquakes[i];

			var depth = -1;
			Location? location = null;
			foreach (var c in earthquake.Hypocenter.Area.Coordinates)
			{
				// 度分 のときは深さだけ更新する
				if (c.Type == "震源位置（度分）")
				{
					depth = CoordinateConverter.GetDepth(c.Value) ?? depth;
					continue;
				}
				location = CoordinateConverter.GetLocation(c.Value);
				depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
			}

			result[i] = (eventIds[i], new HypocenterInformationFragment
			{
				ArrivedTime = report.Head.ReportDateTime.DateTime,
				BasedTelegram = telegram,
				Title = report.Control.Title,
				IsTest = report.Control.Status == "試験",
				IsTraining = report.Control.Status == "訓練",

				OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
				Place = earthquake.Hypocenter.Area.Name,
				Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
				Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
				MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
				Depth = depth,

				Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
				FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
			});
		}
		return result;
	}

	/// <summary>
	/// 中間表現データからFragmentを生成する
	/// </summary>
	public static EarthquakeInformationFragment CreateFromIntermediateData(
		EarthquakeInformationData data)
	{
		var telegram = new ExternalApiTelegram(data.Source, data.Title, data.EventId, data.ReportDateTime);
		var isTest = data.Status == EarthquakeReportStatus.Test;
		var isTraining = data.Status == EarthquakeReportStatus.Training;

		return data.Title switch
		{
			"震源に関する情報" or "顕著な地震の震源要素更新のお知らせ" =>
				CreateHypocenterFragment(data, telegram, isTest, isTraining),
			"震度速報" =>
				CreateIntensityFragment(data, telegram, isTest, isTraining),
			"震源・震度に関する情報" =>
				CreateHypocenterAndIntensityFragment(data, telegram, isTest, isTraining),
			"長周期地震動に関する観測情報" =>
				CreateLpgmFragment(data, telegram, isTest, isTraining),
			_ => throw new EarthquakeInformationFragmentProcessException($"不明な電文タイトルです: {data.Title}"),
		};
	}

	private static HypocenterInformationFragment CreateHypocenterFragment(
		EarthquakeInformationData data, Telegram telegram, bool isTest, bool isTraining)
	{
		if (data.Hypocenter is not { } hypo)
			throw new EarthquakeInformationFragmentProcessException("震源情報がみつかりません");

		return new HypocenterInformationFragment
		{
			ArrivedTime = data.ReportDateTime,
			BasedTelegram = telegram,
			Title = data.Title,
			IsTest = isTest,
			IsTraining = isTraining,
			OccurrenceTime = hypo.OccurrenceTime,
			Place = hypo.Place,
			Location = hypo.Location
				?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
			Magnitude = hypo.Magnitude,
			MagnitudeAlternativeText = hypo.MagnitudeAlternativeText,
			Depth = hypo.Depth,
			Comment = data.ForecastComment,
			FreeFormComment = data.FreeFormComment,
		};
	}

	private static IntensityInformationFragment CreateIntensityFragment(
		EarthquakeInformationData data, Telegram telegram, bool isTest, bool isTraining)
	{
		if (data.Intensity is not { } intensity)
			throw new EarthquakeInformationFragmentProcessException("震度情報がみつかりません");

		return new IntensityInformationFragment
		{
			ArrivedTime = data.ReportDateTime,
			BasedTelegram = telegram,
			Title = data.Title,
			IsTest = isTest,
			IsTraining = isTraining,
			IntensityData = new EarthquakeDisplayIntensityData
			{
				MaxIntensity = intensity.MaxIntensity,
				ObservationPrefs = intensity.ObservationPrefs,
				FlatPoints = intensity.FlatPoints,
			},
			Place = intensity.RepresentativeAreaName
				?? throw new EarthquakeInformationFragmentProcessException("Place がみつかりません"),
			DetectionTime = intensity.DetectionTime
				?? throw new EarthquakeInformationFragmentProcessException("DetectionTime がみつかりません"),
			MaxIntensity = intensity.MaxIntensity,
			IsOnlypoint = intensity.IsOnlyArea,
			Comment = data.ForecastComment,
			FreeFormComment = data.FreeFormComment,
		};
	}

	private static HypocenterAndIntensityInformationFragment CreateHypocenterAndIntensityFragment(
		EarthquakeInformationData data, Telegram telegram, bool isTest, bool isTraining)
	{
		if (data.Hypocenter is not { } hypo)
			throw new EarthquakeInformationFragmentProcessException("震源情報がみつかりません");

		return new HypocenterAndIntensityInformationFragment
		{
			ArrivedTime = data.ReportDateTime,
			BasedTelegram = telegram,
			Title = data.Title,
			IsTest = isTest,
			IsTraining = isTraining,
			IntensityData = data.Intensity != null ? new EarthquakeDisplayIntensityData
			{
				MaxIntensity = data.Intensity.MaxIntensity,
				ObservationPrefs = data.Intensity.ObservationPrefs,
				FlatPoints = data.Intensity.FlatPoints,
			} : null,
			OccurrenceTime = hypo.OccurrenceTime,
			Place = hypo.Place,
			Location = hypo.Location
				?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
			Magnitude = hypo.Magnitude,
			MagnitudeAlternativeText = hypo.MagnitudeAlternativeText,
			Depth = hypo.Depth,
			MaxIntensity = data.Intensity?.MaxIntensity ?? JmaIntensity.Unknown,
			IsForeign = data.IsForeign,
			IsVolcano = data.IsVolcano,
			VolcanoName = data.VolcanoName,
			Comment = data.ForecastComment,
			FreeFormComment = data.FreeFormComment,
		};
	}

	private static LpgmIntensityInformationFragment CreateLpgmFragment(
		EarthquakeInformationData data, Telegram telegram, bool isTest, bool isTraining)
	{
		if (data.Hypocenter is not { } hypo)
			throw new EarthquakeInformationFragmentProcessException("震源情報がみつかりません");

		return new LpgmIntensityInformationFragment
		{
			ArrivedTime = data.ReportDateTime,
			BasedTelegram = telegram,
			Title = data.Title,
			IsTest = isTest,
			IsTraining = isTraining,
			IntensityData = data.Intensity != null ? new EarthquakeDisplayIntensityData
			{
				MaxIntensity = data.Intensity.MaxIntensity,
				ObservationPrefs = data.Intensity.ObservationPrefs,
				FlatPoints = data.Intensity.FlatPoints,
			} : null,
			OccurrenceTime = hypo.OccurrenceTime,
			Place = hypo.Place,
			Location = hypo.Location
				?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
			Magnitude = hypo.Magnitude,
			MagnitudeAlternativeText = hypo.MagnitudeAlternativeText,
			Depth = hypo.Depth,
			MaxIntensity = data.Intensity?.MaxIntensity ?? JmaIntensity.Unknown,
			MaxLpgmIntensity = data.MaxLpgmIntensity ?? LpgmIntensity.Unknown,
			IsForeign = false,
			IsVolcano = false,
			Comment = data.ForecastComment,
			FreeFormComment = data.FreeFormComment,
		};
	}

	/// <summary>
	/// 発表時刻
	/// </summary>
	public required DateTime ArrivedTime { get; init; }

	/// <summary>
	/// ベースとなった電文
	/// </summary>
	public required Telegram BasedTelegram { get; init; }

	/// <summary>
	/// 電文名
	/// </summary>
	public required string Title { get; set; }

	/// <summary>
	/// 訓練
	/// </summary>
	public required bool IsTraining { get; init; }

	/// <summary>
	/// 試験
	/// </summary>
	public required bool IsTest { get; init; }

	/// <summary>
	/// 震度観測データ
	/// </summary>
	public EarthquakeDisplayIntensityData? IntensityData { get; init; }

	private bool _isCancelled;
	/// <summary>
	/// 情報が取り消されたか
	/// </summary>
	public bool IsCancelled
	{
		get => _isCancelled;
		set => this.RaiseAndSetIfChanged(ref _isCancelled, value);
	}

	private bool _isCorrected;
	/// <summary>
	/// 情報が訂正済みか
	/// </summary>
	public bool IsCorrected
	{
		get => _isCorrected;
		set => this.RaiseAndSetIfChanged(ref _isCorrected, value);
	}
}

/// <summary>
/// 震源情報･顕著な地震の震源要素更新のお知らせ
/// </summary>
public class HypocenterInformationFragment : EarthquakeInformationFragment
{
	/// <summary>
	/// 発生時刻
	/// </summary>
	public required DateTime OccurrenceTime { get; init; }

	/// <summary>
	/// 震央
	/// </summary>
	public required string Place { get; init; }

	/// <summary>
	/// 震央座標
	/// </summary>
	public required Location Location { get; init; }

	/// <summary>
	/// マグニチュード
	/// </summary>
	public required float Magnitude { get; init; }

	/// <summary>
	/// マグニチュードの代替テキスト
	/// </summary>
	public required string? MagnitudeAlternativeText { get; init; }

	/// <summary>
	/// 深さ(km)
	/// </summary>
	public required int Depth { get; init; }

	/// <summary>
	/// 固定付加文
	/// </summary>
	public string? Comment { get; init; }

	/// <summary>
	/// 自由形式文
	/// </summary>
	public string? FreeFormComment { get; init; }
}

/// <summary>
/// 震源･震度情報
/// </summary>
public class HypocenterAndIntensityInformationFragment : HypocenterInformationFragment
{
	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 海外で発生した地震か
	/// </summary>
	public required bool IsForeign { get; init; }

	/// <summary>
	/// 大規模な噴火か
	/// </summary>
	public required bool IsVolcano { get; init; }

	/// <summary>
	/// 噴火の場合の火山名
	/// </summary>
	public string? VolcanoName { get; init; }
}

/// <summary>
/// 震度速報
/// </summary>
public class IntensityInformationFragment : EarthquakeInformationFragment
{
	/// <summary>
	/// 代表地域
	/// </summary>
	public required string Place { get; init; }

	/// <summary>
	/// 検知時刻
	/// </summary>
	public required DateTime DetectionTime { get; init; }

	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 発表地域が1つのみか
	/// </summary>
	public bool IsOnlypoint { get; init; }

	/// <summary>
	/// 固定付加文
	/// </summary>
	public string? Comment { get; init; }

	/// <summary>
	/// 自由形式文
	/// </summary>
	public string? FreeFormComment { get; init; }
}

/// <summary>
/// 長周期
/// </summary>
public class LpgmIntensityInformationFragment : HypocenterAndIntensityInformationFragment
{
	/// <summary>
	/// 最大の長周期地震動階級
	/// </summary>
	public required LpgmIntensity MaxLpgmIntensity { get; init; }
}

/// <summary>
/// 推計震度分布
/// </summary>
//public class EstimatedIntensityDistributionInformationFragment : EarthquakeInformationFragment
//{
//	// TODO: 未実装
//}
