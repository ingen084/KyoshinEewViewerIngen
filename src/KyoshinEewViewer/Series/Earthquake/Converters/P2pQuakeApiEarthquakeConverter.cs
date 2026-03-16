using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;
using KyoshinMonitorLib;
using Splat;
using System;
using System.Globalization;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Converters;

/// <summary>
/// P2P地震情報 APIの地震情報メッセージから中間表現への変換
/// </summary>
internal static class P2pQuakeApiEarthquakeConverter
{

	/// <summary>
	/// P2P地震情報メッセージを中間表現に変換する
	/// </summary>
	/// <returns>変換結果。対応していないタイプの場合はnull</returns>
	public static EarthquakeInformationData? Convert(P2pQuakeApiEarthquakeMessage message)
	{
		if (message.Issue == null)
			return null;

		var isForeign = message.Issue.Type == "Foreign";

		if (message.Issue.Type switch
		{
			"ScalePrompt" => "震度速報",
			"Destination" => "震源に関する情報",
			"ScaleAndDestination" or "DetailScale" or "Foreign" => "震源・震度に関する情報",
			_ => null,
		} is not string title)
			return null;

		var eventId = $"p2p:{message.Id}";

		if (!DateTime.TryParse(message.Issue.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var reportDateTime))
			reportDateTime = DateTime.Now;

		return title switch
		{
			"震度速報" => ConvertIntensityReport(message, eventId, reportDateTime),
			"震源に関する情報" => ConvertHypocenterInfo(message, eventId, reportDateTime),
			"震源・震度に関する情報" => ConvertHypocenterAndIntensityInfo(message, eventId, reportDateTime, isForeign),
			_ => null,
		};
	}

	/// <summary>
	/// 震度速報
	/// </summary>
	private static EarthquakeInformationData? ConvertIntensityReport(
		P2pQuakeApiEarthquakeMessage message, string eventId, DateTime reportDateTime)
	{
		if (message.Earthquake == null)
			return null;

		var maxIntensity = P2pQuakeApiScaleConverter.ToJmaIntensity(message.Earthquake.MaxScale);

		// ポイント配列から最大震度のエリアを抽出
		string? representativeAreaName = null;
		var isOnlyArea = true;
		if (message.Points != null)
		{
			var maxScalePoints = message.Points
				.Where(p => p.IsArea && P2pQuakeApiScaleConverter.ToJmaIntensity(p.Scale) == maxIntensity)
				.ToArray();

			if (maxScalePoints.Length > 0)
				representativeAreaName = maxScalePoints[0].Addr;
			if (maxScalePoints.Length > 1)
				isOnlyArea = false;
		}

		if (!DateTime.TryParse(message.Earthquake.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var detectionTime))
			detectionTime = reportDateTime;

		return new EarthquakeInformationData
		{
			Title = "震度速報",
			EventId = eventId,
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime,
			Source = "P2pQuakeApi",
			TelegramKey = $"P2pQuakeApi:{message.Id}",
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
				DetectionTime = detectionTime,
				RepresentativeAreaName = representativeAreaName ?? "不明",
				IsOnlyArea = isOnlyArea,
			},
		};
	}

	/// <summary>
	/// 震源に関する情報
	/// </summary>
	private static EarthquakeInformationData? ConvertHypocenterInfo(
		P2pQuakeApiEarthquakeMessage message, string eventId, DateTime reportDateTime)
	{
		if (message.Earthquake?.Hypocenter == null)
			return null;

		var hypo = message.Earthquake.Hypocenter;
		var (location, depth, magnitude) = ExtractHypocenterInfo(hypo, message.Earthquake);

		if (!DateTime.TryParse(message.Earthquake.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurrenceTime))
			occurrenceTime = reportDateTime;

		return new EarthquakeInformationData
		{
			Title = "震源に関する情報",
			EventId = eventId,
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime,
			Source = "P2pQuakeApi",
			TelegramKey = $"P2pQuakeApi:{message.Id}",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = occurrenceTime,
				Place = hypo.Name ?? "不明",
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? "不明" : null,
				Depth = depth,
			},
		};
	}

	/// <summary>
	/// 震源・震度に関する情報（遠地地震情報を含む）
	/// </summary>
	private static EarthquakeInformationData? ConvertHypocenterAndIntensityInfo(
		P2pQuakeApiEarthquakeMessage message, string eventId, DateTime reportDateTime, bool isForeign = false)
	{
		if (message.Earthquake?.Hypocenter == null)
			return null;

		var hypo = message.Earthquake.Hypocenter;
		var (location, depth, magnitude) = ExtractHypocenterInfo(hypo, message.Earthquake);
		var maxIntensity = P2pQuakeApiScaleConverter.ToJmaIntensity(message.Earthquake.MaxScale);

		if (!DateTime.TryParse(message.Earthquake.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurrenceTime))
			occurrenceTime = reportDateTime;

		return new EarthquakeInformationData
		{
			Title = "震源・震度に関する情報",
			EventId = eventId,
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime,
			Source = "P2pQuakeApi",
			TelegramKey = $"P2pQuakeApi:{message.Id}",
			IsForeign = isForeign,
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = occurrenceTime,
				Place = hypo.Name ?? "不明",
				Location = location,
				Magnitude = magnitude,
				MagnitudeAlternativeText = float.IsNaN(magnitude) ? "不明" : null,
				Depth = depth,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
			},
		};
	}

	/// <summary>
	/// 震源情報を抽出する
	/// </summary>
	private static (Location? Location, int Depth, float Magnitude) ExtractHypocenterInfo(
		P2pQuakeApiEarthquakeHypocenter hypo, P2pQuakeApiEarthquakeDetail earthquake)
	{
		Location? location = null;
		if (hypo.Latitude != -200 && hypo.Longitude != -200)
			location = new Location((float)hypo.Latitude, (float)hypo.Longitude);

		var depth = hypo.Depth == -1 ? -1 : (int)hypo.Depth;
		var magnitude = earthquake.MaxScale == -1 ? float.NaN : (float)hypo.Magnitude;
		if (hypo.Magnitude == -1)
			magnitude = float.NaN;

		return (location, depth, magnitude);
	}
}
