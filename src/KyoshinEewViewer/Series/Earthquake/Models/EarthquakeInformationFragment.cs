using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinMonitorLib;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public abstract partial class EarthquakeInformationFragment : ObservableObject
{
	[GeneratedRegex("(.+)（日本時間）に(.+)で大規模な噴火が発生しました")]
	private static partial Regex VolcanoMatchRegex();

	/// <summary>
	/// 震源位置の座標群をパースする。<br/>
	/// 「震源位置（度分）」が存在する場合はそちらを優先し、無ければ通常の「震源位置」を採用する。
	/// 誤差は座標表現の精度から推定する。
	/// </summary>
	private static (Location? Location, Location? LocationError, int Depth, int? DepthError) ParseHypocenterCoordinates(HypocenterArea area)
	{
		Location? location = null;
		Location? locationError = null;
		var depth = -1;
		int? depthError = null;

		Location? degreeMinuteLocation = null;
		Location? degreeMinuteLocationError = null;
		var degreeMinuteDepth = -1;
		int? degreeMinuteDepthError = null;
		var hasDegreeMinute = false;

		foreach (var c in area.Coordinates)
		{
			if (c.Type == "震源位置（度分）")
			{
				hasDegreeMinute = true;
				// 度分形式は緯度経度の精度が約1分(=1/60°)、深さは 1km 精度
				degreeMinuteLocation = CoordinateConverter.GetLocationFromDegreeMinute(c.Value);
				degreeMinuteLocationError = new Location(0.5f / 60f, 0.5f / 60f);
				degreeMinuteDepth = CoordinateConverter.GetDepth(c.Value) ?? degreeMinuteDepth;
				degreeMinuteDepthError = 1;
				continue;
			}
			location = CoordinateConverter.GetLocation(c.Value);
			// 通常形式は 0.1° 単位・深さは 10km 単位で報じられる想定
			locationError = new Location(0.05f, 0.05f);
			depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
			depthError = 10;
		}

		// 度分形式が存在する場合は優先して採用する
		if (hasDegreeMinute)
		{
			if (degreeMinuteLocation != null)
			{
				location = degreeMinuteLocation;
				locationError = degreeMinuteLocationError;
			}
			if (degreeMinuteDepth >= 0)
			{
				depth = degreeMinuteDepth;
				depthError = degreeMinuteDepthError;
			}
		}
		return (location, locationError, depth, depthError);
	}

	// メモ　取り消しは上位でやる
	public static EarthquakeInformationFragment CreateFromJmxXmlDocument(Telegram telegram, JmaXmlDocument report)
	{
		switch (report.Control.Title)
		{
			case "震源に関する情報":
			case "顕著な地震の震源要素更新のお知らせ":
				{
					if (report.EarthquakeBody.Earthquake is not { } earthquake)
						throw new EarthquakeInformationFragmentProcessException("Earthquake がみつかりません");

					var (location, locationError, depth, depthError) = ParseHypocenterCoordinates(earthquake.Hypocenter.Area);

					return new HypocenterInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegram = telegram,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
						Place = earthquake.Hypocenter.Area.Name,
						Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
						MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
						Depth = depth,
						DepthError = depthError,
						Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
						LocationError = locationError,

						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			case "震度速報":
				{
					if (report.EarthquakeBody.Intensity?.Observation is not { } observation)
						throw new EarthquakeWatchException("Observation がみつかりません");

					string? areaName = null;
					var isOnlyPosition = true;
					foreach (var pref in observation.Prefs)
					{
						// すでに複数件存在することが判明していれば戻る
						if (!isOnlyPosition)
							break;
						foreach (var area in pref.Areas)
						{
							// すでに area の取得ができていれば複数箇所存在するフラグを立てる
							if (areaName != null && isOnlyPosition)
							{
								isOnlyPosition = false;
								break;
							}
							// 未取得であれば area に代入
							areaName = area.Name;
						}
					}

					return new IntensityInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegram = telegram,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						Place = areaName
							?? throw new EarthquakeInformationFragmentProcessException("Place がみつかりません"),
						DetectionTime = report.Head.TargetDateTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("TargetDateTime がみつかりません"),
						MaxIntensity = observation.MaxInt?.ToJmaIntensity()
							?? throw new EarthquakeInformationFragmentProcessException("MaxIntensity がみつかりません"),
						IsOnlypoint = isOnlyPosition,
						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			case "震源・震度に関する情報":
				{
					if (report.EarthquakeBody.Earthquake is not { } earthquake)
						throw new EarthquakeInformationFragmentProcessException("Earthquake がみつかりません");

					var (location, locationError, depth, depthError) = ParseHypocenterCoordinates(earthquake.Hypocenter.Area);

					MatchCollection? volcanoMatches = null;
					if (report.EarthquakeBody.Comments?.FreeFormComment is string fc)
						volcanoMatches = VolcanoMatchRegex().Matches(fc);

					return new HypocenterAndIntensityInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegram = telegram,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
						Place = earthquake.Hypocenter.Area.Name,
						Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
						MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
						Depth = depth,
						DepthError = depthError,
						Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
						LocationError = locationError,

						MaxIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
						IsForeign = report.Head.Title == "遠地地震に関する情報",
						IsVolcano = (volcanoMatches?.Count ?? 0) > 0,
						VolcanoName = volcanoMatches?.FirstOrDefault()?.Groups[2].Value,

						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			case "長周期地震動に関する観測情報":
				{
					if (report.EarthquakeBody.Earthquake is not { } earthquake)
						throw new EarthquakeInformationFragmentProcessException("Earthquake がみつかりません");

					var (location, locationError, depth, depthError) = ParseHypocenterCoordinates(earthquake.Hypocenter.Area);

					return new LpgmIntensityInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegram = telegram,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
						Place = earthquake.Hypocenter.Area.Name,
						Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
						MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
						Depth = depth,
						DepthError = depthError,
						Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
						LocationError = locationError,

						MaxIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
						MaxLpgmIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxLgInt?.ToLpgmIntensity() ?? LpgmIntensity.Unknown,
						IsForeign = false,
						IsVolcano = false,

						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			default:
				throw new EarthquakeInformationFragmentProcessException($"不明な電文タイトルです: {report.Control.Title}");
		}

	}

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

			var (location, locationError, depth, depthError) = ParseHypocenterCoordinates(earthquake.Hypocenter.Area);

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
				LocationError = locationError,
				Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
				MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
				Depth = depth,
				DepthError = depthError,

				Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
				FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
			});
		}
		return result;
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
	/// 情報が取り消されたか
	/// </summary>
	[ObservableProperty]
	public partial bool IsCancelled { get; set; }

	/// <summary>
	/// 情報が訂正済みか
	/// </summary>
	[ObservableProperty]
	public partial bool IsCorrected { get; set; }
}

/// <summary>
/// 震源情報･顕著な地震の震源要素更新のお知らせ
/// </summary>
public partial class HypocenterInformationFragment : EarthquakeInformationFragment
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
	/// 震央座標の誤差 (±度)<br/>
	/// 座標表現の精度から推定される値。null の場合は誤差情報が得られない。
	/// </summary>
	public Location? LocationError { get; init; }

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
	/// 深さの誤差 (±km)<br/>
	/// null の場合は誤差情報が得られない。
	/// </summary>
	public int? DepthError { get; init; }

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
public partial class HypocenterAndIntensityInformationFragment : HypocenterInformationFragment
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
public partial class IntensityInformationFragment : EarthquakeInformationFragment
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
public partial class LpgmIntensityInformationFragment : HypocenterAndIntensityInformationFragment
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
