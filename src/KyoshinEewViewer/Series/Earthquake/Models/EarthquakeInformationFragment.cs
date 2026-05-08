using DmdataSharp.ApiResponses.V2.Parameters;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public abstract partial class EarthquakeInformationFragment
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
	public static EarthquakeInformationFragment CreateFromJmxXmlDocument(
		Telegram telegram,
		JmaXmlDocument report,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations = null)
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
					var isSingleArea = true;
					foreach (var pref in observation.Prefs)
					{
						if (!isSingleArea)
							break;
						foreach (var area in pref.Areas)
						{
							if (areaName != null && isSingleArea)
							{
								isSingleArea = false;
								break;
							}
							areaName = area.Name;
						}
					}

					return new IntensityInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						Place = areaName
							?? throw new EarthquakeInformationFragmentProcessException("Place がみつかりません"),
						DetectionTime = report.Head.TargetDateTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("TargetDateTime がみつかりません"),
						MaxIntensity = observation.MaxInt?.ToJmaIntensity()
							?? throw new EarthquakeInformationFragmentProcessException("MaxIntensity がみつかりません"),
						IsSingleArea = isSingleArea,
						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
						Observation = BuildObservationTree(report, EarthquakeIntensityScope.AreaOnly, stations),
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
						Observation = BuildObservationTree(report, EarthquakeIntensityScope.Detail, stations),
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
						Observation = BuildObservationTree(report, EarthquakeIntensityScope.Lpgm, stations),
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
	/// 観測点ツリー (Pref→Area→City→Station) を構築する
	/// </summary>
	internal static IReadOnlyList<PrefectureIntensitySnapshot> BuildObservationTree(
		JmaXmlDocument report,
		EarthquakeIntensityScope scope,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations)
	{
		if (report.EarthquakeBody.Intensity?.Observation is not { } observation)
			return [];

		var prefs = new List<PrefectureIntensitySnapshot>();
		foreach (var pref in observation.Prefs)
		{
			var areas = new List<AreaIntensitySnapshot>();
			foreach (var area in pref.Areas)
			{
				var cities = new List<CityIntensitySnapshot>();
				foreach (var city in area.Cities)
				{
					var stationsList = new List<StationIntensitySnapshot>();
					foreach (var st in city.IntensityStations)
					{
						var stInfo = stations?.FirstOrDefault(s => s.Code == st.Code);

						IReadOnlyList<LgIntByPeriod>? lpgmByPeriod = null;
						LpgmIntensity? maxLpgm = null;
						if (scope == EarthquakeIntensityScope.Lpgm)
						{
							var pairs = st.LgIntPerPeriods
								.Select(p => new LgIntByPeriod(p.PeriodicBand, p.Value.ToLpgmIntensity()))
								.ToArray();
							if (pairs.Length > 0)
							{
								lpgmByPeriod = pairs;
								maxLpgm = pairs.Max(p => p.Intensity);
							}
						}

						stationsList.Add(new StationIntensitySnapshot(
							Name: string.Intern(st.Name),
							Code: int.TryParse(st.Code, NumberStyles.None, CultureInfo.InvariantCulture, out var stCode) ? stCode : 0,
							Intensity: st.Int.ToJmaIntensity(),
							MaxLpgmIntensity: maxLpgm,
							LpgmByPeriod: lpgmByPeriod,
							Location: stInfo?.GetLocation()));
					}
					cities.Add(new CityIntensitySnapshot(
						Name: string.Intern(city.Name),
						Code: city.Code,
						MaxIntensity: city.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
						MaxLpgmIntensity: city.MaxLgInt?.ToLpgmIntensity(),
						CenterLocation: RegionCenterLocations.Default.GetLocation(LandLayerType.MunicipalityEarthquakeTsunamiArea, city.Code),
						Stations: stationsList));
				}
				areas.Add(new AreaIntensitySnapshot(
					Name: string.Intern(area.Name),
					Code: area.Code,
					MaxIntensity: area.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
					MaxLpgmIntensity: area.MaxLgInt?.ToLpgmIntensity(),
					CenterLocation: RegionCenterLocations.Default.GetLocation(LandLayerType.EarthquakeInformationSubdivisionArea, area.Code),
					Cities: cities));
			}
			prefs.Add(new PrefectureIntensitySnapshot(
				Name: string.Intern(pref.Name),
				Code: pref.Code,
				MaxIntensity: pref.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
				MaxLpgmIntensity: pref.MaxLgInt?.ToLpgmIntensity(),
				Areas: areas));
		}
		return prefs;
	}

	/// <summary>
	/// 発表時刻
	/// </summary>
	public required DateTime ArrivedTime { get; init; }

	/// <summary>
	/// 電文名
	/// </summary>
	public required string Title { get; init; }

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
	public bool IsCancelled { get; set; }

	/// <summary>
	/// 情報が訂正済みか
	/// </summary>
	public bool IsCorrected { get; set; }
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

	/// <summary>
	/// 観測点ツリー
	/// </summary>
	public required IReadOnlyList<PrefectureIntensitySnapshot> Observation { get; init; }
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
	public bool IsSingleArea { get; init; }

	/// <summary>
	/// 固定付加文
	/// </summary>
	public string? Comment { get; init; }

	/// <summary>
	/// 自由形式文
	/// </summary>
	public string? FreeFormComment { get; init; }

	/// <summary>
	/// 観測点ツリー
	/// </summary>
	public required IReadOnlyList<PrefectureIntensitySnapshot> Observation { get; init; }
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
