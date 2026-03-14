using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.Earthquake.Converters;

/// <summary>
/// JMA XML電文から中間表現への変換
/// </summary>
internal static partial class JmaXmlEarthquakeConverter
{
	[GeneratedRegex("(.+)（日本時間）に(.+)で大規模な噴火が発生しました")]
	private static partial Regex VolcanoMatchRegex();

	/// <summary>
	/// JMA XML電文を中間表現に変換する
	/// </summary>
	/// <returns>変換結果。対応していない電文タイトルの場合はnull</returns>
	public static EarthquakeInformationData? Convert(JmaXmlDocument report)
	{
		var status = report.Control.Status switch
		{
			"訓練" => EarthquakeReportStatus.Training,
			"試験" => EarthquakeReportStatus.Test,
			_ => EarthquakeReportStatus.Normal,
		};

		var infoType = report.Head.InfoType switch
		{
			"取消" => EarthquakeInfoType.Cancel,
			"訂正" => EarthquakeInfoType.Correction,
			_ => EarthquakeInfoType.Normal,
		};

		return report.Control.Title switch
		{
			"震源に関する情報" or "顕著な地震の震源要素更新のお知らせ" =>
				ConvertHypocenterInfo(report, status, infoType),
			"震度速報" =>
				ConvertIntensityReport(report, status, infoType),
			"震源・震度に関する情報" =>
				ConvertHypocenterAndIntensityInfo(report, status, infoType),
			"長周期地震動に関する観測情報" =>
				ConvertLpgmInfo(report, status, infoType),
			_ => null,
		};
	}

	/// <summary>
	/// 震源に関する情報・顕著な地震の震源要素更新のお知らせ
	/// </summary>
	private static EarthquakeInformationData ConvertHypocenterInfo(
		JmaXmlDocument report, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		if (report.EarthquakeBody.Earthquake is not { } earthquake)
			throw new EarthquakeConverterException("Earthquake がみつかりません");

		var (location, depth) = ExtractHypocenter(earthquake);

		if (!earthquake.Magnitude.TryGetFloatValue(out var magnitude))
			throw new EarthquakeConverterException("Magnitude がfloatにパースできません");

		return new EarthquakeInformationData
		{
			Title = report.Control.Title,
			EventId = report.Head.EventId,
			InfoType = infoType,
			Status = status,
			ReportDateTime = report.Head.ReportDateTime.DateTime,
			Source = "JmaXml",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = earthquake.OriginTime?.DateTime
					?? throw new EarthquakeConverterException("OccurrenceTime がみつかりません"),
				Place = earthquake.Hypocenter.Area.Name,
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? earthquake.Magnitude.Description : null,
				Depth = depth,
			},
			ForecastComment = report.EarthquakeBody.Comments?.ForecastCommentText,
			FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
		};
	}

	/// <summary>
	/// 震度速報
	/// </summary>
	private static EarthquakeInformationData ConvertIntensityReport(
		JmaXmlDocument report, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		if (report.EarthquakeBody.Intensity?.Observation is not { } observation)
			throw new EarthquakeConverterException("Observation がみつかりません");

		var maxIntensity = observation.MaxInt?.ToJmaIntensity()
			?? throw new EarthquakeConverterException("MaxIntensity がみつかりません");

		// 代表地域名と複数地域判定
		string? areaName = null;
		var isOnlyArea = true;
		foreach (var pref in observation.Prefs)
		{
			if (!isOnlyArea)
				break;
			foreach (var area in pref.Areas)
			{
				if (areaName != null && isOnlyArea)
				{
					isOnlyArea = false;
					break;
				}
				areaName = area.Name;
			}
		}

		return new EarthquakeInformationData
		{
			Title = report.Control.Title,
			EventId = report.Head.EventId,
			InfoType = infoType,
			Status = status,
			ReportDateTime = report.Head.ReportDateTime.DateTime,
			Source = "JmaXml",
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
				DetectionTime = report.Head.TargetDateTime?.DateTime,
				RepresentativeAreaName = areaName,
				IsOnlyArea = isOnlyArea,
			},
			ForecastComment = report.EarthquakeBody.Comments?.ForecastCommentText,
			FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
		};
	}

	/// <summary>
	/// 震源・震度に関する情報
	/// </summary>
	private static EarthquakeInformationData ConvertHypocenterAndIntensityInfo(
		JmaXmlDocument report, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		if (report.EarthquakeBody.Earthquake is not { } earthquake)
			throw new EarthquakeConverterException("Earthquake がみつかりません");

		var (location, depth) = ExtractHypocenter(earthquake);

		if (!earthquake.Magnitude.TryGetFloatValue(out var magnitude))
			throw new EarthquakeConverterException("Magnitude がfloatにパースできません");

		MatchCollection? volcanoMatches = null;
		if (report.EarthquakeBody.Comments?.FreeFormComment is string fc)
			volcanoMatches = VolcanoMatchRegex().Matches(fc);

		return new EarthquakeInformationData
		{
			Title = report.Control.Title,
			EventId = report.Head.EventId,
			InfoType = infoType,
			Status = status,
			ReportDateTime = report.Head.ReportDateTime.DateTime,
			Source = "JmaXml",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = earthquake.OriginTime?.DateTime
					?? throw new EarthquakeConverterException("OccurrenceTime がみつかりません"),
				Place = earthquake.Hypocenter.Area.Name,
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? earthquake.Magnitude.Description : null,
				Depth = depth,
			},
			Intensity = report.EarthquakeBody.Intensity?.Observation != null
				? new EarthquakeIntensityData
				{
					MaxIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
				}
				: null,
			IsForeign = report.Head.Title == "遠地地震に関する情報",
			IsVolcano = (volcanoMatches?.Count ?? 0) > 0,
			VolcanoName = volcanoMatches?.FirstOrDefault()?.Groups[2].Value,
			ForecastComment = report.EarthquakeBody.Comments?.ForecastCommentText,
			FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
		};
	}

	/// <summary>
	/// 長周期地震動に関する観測情報
	/// </summary>
	private static EarthquakeInformationData ConvertLpgmInfo(
		JmaXmlDocument report, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		if (report.EarthquakeBody.Earthquake is not { } earthquake)
			throw new EarthquakeConverterException("Earthquake がみつかりません");

		var (location, depth) = ExtractHypocenter(earthquake);

		if (!earthquake.Magnitude.TryGetFloatValue(out var magnitude))
			throw new EarthquakeConverterException("Magnitude がfloatにパースできません");

		return new EarthquakeInformationData
		{
			Title = report.Control.Title,
			EventId = report.Head.EventId,
			InfoType = infoType,
			Status = status,
			ReportDateTime = report.Head.ReportDateTime.DateTime,
			Source = "JmaXml",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = earthquake.OriginTime?.DateTime
					?? throw new EarthquakeConverterException("OccurrenceTime がみつかりません"),
				Place = earthquake.Hypocenter.Area.Name,
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? earthquake.Magnitude.Description : null,
				Depth = depth,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
			},
			MaxLpgmIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxLgInt?.ToLpgmIntensity() ?? LpgmIntensity.Unknown,
			ForecastComment = report.EarthquakeBody.Comments?.ForecastCommentText,
			FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
		};
	}

	/// <summary>
	/// 震源情報の座標・深さを抽出する
	/// </summary>
	private static (Location? Location, int Depth) ExtractHypocenter(JmaXmlParser.Data.Earthquake.EarthquakeData earthquake)
	{
		var depth = -1;
		Location? location = null;
		foreach (var c in earthquake.Hypocenter.Area.Coordinates)
		{
			// 度分のときは深さだけ更新する
			if (c.Type == "震源位置（度分）")
			{
				depth = CoordinateConverter.GetDepth(c.Value) ?? depth;
				continue;
			}
			location = CoordinateConverter.GetLocation(c.Value);
			depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
		}
		return (location, depth);
	}

	/// <summary>
	/// 観測情報の階層構造を構築する（JmaXmlDisplayDataProviderから遅延パース時に呼び出される）
	/// </summary>
	internal static EarthquakeObservationPref[] BuildObservationPrefs(
		JmaXmlParser.Data.Earthquake.IntensityObservation observation, bool onlyAreas)
	{
		var prefs = new List<EarthquakeObservationPref>();

		foreach (var pref in observation.Prefs)
		{
			var areas = new List<EarthquakeObservationArea>();

			foreach (var area in pref.Areas)
			{
				var areaIntensity = area.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

				if (onlyAreas)
				{
					// 震度速報: 区域レベルまで
					areas.Add(new EarthquakeObservationArea
					{
						Name = area.Name,
						Code = area.Code,
						MaxIntensity = areaIntensity,
					});
					continue;
				}

				// 震源・震度情報 / 長周期: 市区町村・観測点レベルまで
				var cities = new List<EarthquakeObservationCity>();

				foreach (var city in area.Cities)
				{
					var cityIntensity = city.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

					var stations = new List<EarthquakeObservationStation>();
					foreach (var station in city.IntensityStations)
					{
						stations.Add(new EarthquakeObservationStation
						{
							Name = station.Name,
							Code = station.Code,
							Intensity = station.Int.ToJmaIntensity(),
						});
					}

					cities.Add(new EarthquakeObservationCity
					{
						Name = city.Name,
						Code = city.Code,
						MaxIntensity = cityIntensity,
						Stations = stations.ToArray(),
					});
				}

				areas.Add(new EarthquakeObservationArea
				{
					Name = area.Name,
					Code = area.Code,
					MaxIntensity = areaIntensity,
					Cities = cities.ToArray(),
				});
			}

			prefs.Add(new EarthquakeObservationPref
			{
				Name = pref.Name,
				Code = pref.Code,
				Areas = areas.ToArray(),
			});
		}

		return prefs.ToArray();
	}
}

/// <summary>
/// JMA XMLコンバータ処理例外
/// </summary>
public class EarthquakeConverterException(string message) : Exception(message);
