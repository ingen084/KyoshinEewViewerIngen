using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using Splat;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AxisEarthquake = KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message.Earthquake;

namespace KyoshinEewViewer.Series.Earthquake.Converters;

/// <summary>
/// AXIS JSON電文から中間表現への変換
/// </summary>
internal static partial class AxisEarthquakeConverter
{
	[GeneratedRegex("(.+)（日本時間）に(.+)で大規模な噴火が発生しました")]
	private static partial Regex VolcanoMatchRegex();

	/// <summary>
	/// AXIS地震情報メッセージを中間表現に変換する
	/// </summary>
	/// <returns>変換結果。対応していない電文タイトルの場合はnull</returns>
	public static EarthquakeInformationData? Convert(EarthquakeMessage message)
	{
		var status = message.Control.Status switch
		{
			"訓練" => EarthquakeReportStatus.Training,
			"試験" => EarthquakeReportStatus.Test,
			_ => EarthquakeReportStatus.Normal,
		};

		var infoType = message.Head.InfoType switch
		{
			"取消" => EarthquakeInfoType.Cancel,
			"訂正" => EarthquakeInfoType.Correction,
			_ => EarthquakeInfoType.Normal,
		};

		return message.Control.Title switch
		{
			"震源に関する情報" or "顕著な地震の震源要素更新のお知らせ" =>
				ConvertHypocenterInfo(message, status, infoType),
			"震度速報" =>
				ConvertIntensityReport(message, status, infoType),
			"震源・震度に関する情報" =>
				ConvertHypocenterAndIntensityInfo(message, status, infoType),
			"長周期地震動に関する観測情報" =>
				ConvertLpgmInfo(message, status, infoType),
			_ => null,
		};
	}

	/// <summary>
	/// 震源に関する情報・顕著な地震の震源要素更新のお知らせ
	/// </summary>
	private static EarthquakeInformationData? ConvertHypocenterInfo(
		EarthquakeMessage message, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		var earthquake = message.Body.Earthquake?.FirstOrDefault();
		if (earthquake == null)
		{
			LogHost.Default.Warn("AXIS地震情報: Earthquake がみつかりません");
			return null;
		}

		var (location, depth) = ExtractHypocenter(earthquake);
		var magnitude = ExtractMagnitude(earthquake);

		return new EarthquakeInformationData
		{
			Title = message.Control.Title,
			EventId = message.Head.EventID,
			InfoType = infoType,
			Status = status,
			ReportDateTime = message.Head.ReportDateTime,
			Source = "Axis",
			TelegramKey = message.Uuid,
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = earthquake.OriginTime,
				Place = earthquake.Hypocenter?.Area?.Name ?? "不明",
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? GetMagnitudeDescription(earthquake) : null,
				Depth = depth,
			},
			ForecastComment = message.Body.Comments?.ForecastComment?.Text,
			FreeFormComment = message.Body.Comments?.VarComment?.Text,
		};
	}

	/// <summary>
	/// 震度速報
	/// </summary>
	private static EarthquakeInformationData? ConvertIntensityReport(
		EarthquakeMessage message, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		if (message.Body.Intensity?.Observation == null)
		{
			LogHost.Default.Warn("AXIS地震情報: Observation がみつかりません");
			return null;
		}

		var observation = message.Body.Intensity.Observation;
		var maxIntensity = observation.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

		// 代表地域名と複数地域判定
		string? areaName = null;
		var isOnlyArea = true;
		if (observation.Pref != null)
		{
			foreach (var pref in observation.Pref)
			{
				if (!isOnlyArea)
					break;
				if (pref.Area == null)
					continue;
				foreach (var area in pref.Area)
				{
					if (areaName != null && isOnlyArea)
					{
						isOnlyArea = false;
						break;
					}
					areaName = area.Name;
				}
			}
		}

		return new EarthquakeInformationData
		{
			Title = message.Control.Title,
			EventId = message.Head.EventID,
			InfoType = infoType,
			Status = status,
			ReportDateTime = message.Head.ReportDateTime,
			Source = "Axis",
			TelegramKey = message.Uuid,
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
				DetectionTime = message.Head.TargetDateTime,
				RepresentativeAreaName = areaName,
				IsOnlyArea = isOnlyArea,
				ObservationPrefs = observation.Pref != null
					? AxisObservationBuilder.BuildObservationPrefs(observation, onlyAreas: true)
					: null,
			},
			ForecastComment = message.Body.Comments?.ForecastComment?.Text,
			FreeFormComment = message.Body.Comments?.VarComment?.Text,
		};
	}

	/// <summary>
	/// 震源・震度に関する情報
	/// </summary>
	private static EarthquakeInformationData? ConvertHypocenterAndIntensityInfo(
		EarthquakeMessage message, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		var earthquake = message.Body.Earthquake?.FirstOrDefault();
		if (earthquake == null)
		{
			LogHost.Default.Warn("AXIS地震情報: Earthquake がみつかりません");
			return null;
		}

		var (location, depth) = ExtractHypocenter(earthquake);
		var magnitude = ExtractMagnitude(earthquake);

		MatchCollection? volcanoMatches = null;
		if (message.Body.Comments?.VarComment?.Text is { } freeForm)
			volcanoMatches = VolcanoMatchRegex().Matches(freeForm);

		var observation = message.Body.Intensity?.Observation;
		var maxIntensity = observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

		return new EarthquakeInformationData
		{
			Title = message.Control.Title,
			EventId = message.Head.EventID,
			InfoType = infoType,
			Status = status,
			ReportDateTime = message.Head.ReportDateTime,
			Source = "Axis",
			TelegramKey = message.Uuid,
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = earthquake.OriginTime,
				Place = earthquake.Hypocenter?.Area?.Name ?? "不明",
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? GetMagnitudeDescription(earthquake) : null,
				Depth = depth,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
				ObservationPrefs = observation?.Pref != null
					? AxisObservationBuilder.BuildObservationPrefs(observation, onlyAreas: false)
					: null,
			},
			IsForeign = message.Head.Title == "遠地地震に関する情報",
			IsVolcano = (volcanoMatches?.Count ?? 0) > 0,
			VolcanoName = volcanoMatches?.FirstOrDefault()?.Groups[2].Value,
			ForecastComment = message.Body.Comments?.ForecastComment?.Text,
			FreeFormComment = message.Body.Comments?.VarComment?.Text,
		};
	}

	/// <summary>
	/// 長周期地震動に関する観測情報
	/// </summary>
	private static EarthquakeInformationData? ConvertLpgmInfo(
		EarthquakeMessage message, EarthquakeReportStatus status, EarthquakeInfoType infoType)
	{
		var earthquake = message.Body.Earthquake?.FirstOrDefault();
		if (earthquake == null)
		{
			LogHost.Default.Warn("AXIS地震情報: Earthquake がみつかりません");
			return null;
		}

		var (location, depth) = ExtractHypocenter(earthquake);
		var magnitude = ExtractMagnitude(earthquake);

		var observation = message.Body.Intensity?.Observation;

		return new EarthquakeInformationData
		{
			Title = message.Control.Title,
			EventId = message.Head.EventID,
			InfoType = infoType,
			Status = status,
			ReportDateTime = message.Head.ReportDateTime,
			Source = "Axis",
			TelegramKey = message.Uuid,
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = earthquake.OriginTime,
				Place = earthquake.Hypocenter?.Area?.Name ?? "不明",
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? GetMagnitudeDescription(earthquake) : null,
				Depth = depth,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
				ObservationPrefs = observation?.Pref != null
					? AxisObservationBuilder.BuildObservationPrefs(observation, onlyAreas: false)
					: null,
			},
			ForecastComment = message.Body.Comments?.ForecastComment?.Text,
			FreeFormComment = message.Body.Comments?.VarComment?.Text,
		};
	}

	/// <summary>
	/// 震源情報の座標・深さを抽出する（ISO 6709形式）
	/// </summary>
	private static (Location? Location, int Depth) ExtractHypocenter(AxisEarthquake earthquake)
	{
		var depth = -1;
		Location? location = null;

		if (earthquake.Hypocenter?.Area?.Coordinate == null)
			return (location, depth);

		foreach (var c in earthquake.Hypocenter.Area.Coordinate)
		{
			if (c.Type == "震源位置（度分）")
			{
				depth = CoordinateConverter.GetDepth(c.ValueOf) ?? depth;
				continue;
			}
			location = CoordinateConverter.GetLocation(c.ValueOf);
			depth = CoordinateConverter.GetDepth(c.ValueOf) ?? -1;
		}
		return (location, depth);
	}

	/// <summary>
	/// マグニチュードを抽出する
	/// </summary>
	private static float ExtractMagnitude(AxisEarthquake earthquake)
	{
		if (earthquake.Magnitude == null || earthquake.Magnitude.Length == 0)
			return float.NaN;

		var mag = earthquake.Magnitude[0];
		if (float.TryParse(mag.ValueOf, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var magnitude))
			return magnitude;

		return float.NaN;
	}

	/// <summary>
	/// マグニチュードの代替テキストを取得する
	/// </summary>
	private static string? GetMagnitudeDescription(AxisEarthquake earthquake)
	{
		if (earthquake.Magnitude == null || earthquake.Magnitude.Length == 0)
			return null;
		return earthquake.Magnitude[0].Description;
	}
}
