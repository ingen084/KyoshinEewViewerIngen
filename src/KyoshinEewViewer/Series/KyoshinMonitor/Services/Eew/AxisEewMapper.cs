using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using EewModel = KyoshinEewViewer.Series.KyoshinMonitor.Models.Eew;
using EewHypocenterModel = KyoshinEewViewer.Series.KyoshinMonitor.Models.EewHypocenter;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using System;
using System.Globalization;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

internal enum AxisEewMessageKind
{
	Ignore,
	Eew,
	Cancel,
}

internal readonly record struct AxisEewMappingResult(
	AxisEewMessageKind Kind,
	EewModel? Eew,
	string? EventId,
	DateTime ReportTime)
{
	public static AxisEewMappingResult Ignore { get; } = new(AxisEewMessageKind.Ignore, null, null, default);
}

internal static class AxisEewMapper
{
	internal static AxisEewMappingResult Map(EewMessage? message)
	{
		if (message is not { EventID: { } eventId, Flag: { } flag } || flag.IsTraining)
			return AxisEewMappingResult.Ignore;

		var reportTime = ToJstDateTime(message.ReportDateTime);
		if (flag.IsCancel)
			return new(AxisEewMessageKind.Cancel, null, eventId, reportTime);

		var depth = ResolveDepth(message.Hypocenter?.Depth);
		var magnitude = ResolveMagnitude(message.Magnitude);
		var forecastIntensities = (message.Forecast ?? [])
			.Select(f => (f.Code, f.Name, Intensity: ResolveForecastIntensity(f.Intensity)))
			.Where(f => f.Intensity != JmaIntensity.Unknown)
			.GroupBy(f => f.Code)
			.Select(g => g.OrderByDescending(f => f.Intensity).First())
			.ToArray();

		var isWarning = (message.Title?.Contains("警報", StringComparison.Ordinal) ?? false) ||
						(message.Text?.Contains("強い揺れ", StringComparison.Ordinal) ?? false);

		var eew = new EewModel
		{
			Id = eventId,
			Source = EewSource.Axis,
			DisplaySource = "AXIS",
			ReceiveTime = reportTime,
			SerialNo = message.Serial,
			IsFinal = flag.IsFinal,
			MaxIntensity = ResolveIntensity(message.Intensity),
			// AXIS の EEW には「以上」に相当する情報がないため IsIntensityOver は設定しない。
			Hypocenter = new EewHypocenterModel
			{
				OccurrenceTime = ToJstDateTime(message.OriginDateTime),
				Place = message.Hypocenter?.Name,
				Location = message.Hypocenter?.Coordinate is { Length: >= 2 } coordinate
					? new Location(coordinate[1], coordinate[0])
					: null,
				Magnitude = magnitude,
				Depth = depth,
				// AXIS の EEW には仮定震源要素のフラグがないため、PLUM 法の初期値を目安に判定する。
				IsTemporary = depth == 10 && magnitude is { } m && Math.Abs(m - 1.0f) < 0.01f,
			},
			IntensityForecastMap = forecastIntensities.Length > 0
				? forecastIntensities.ToDictionary(f => f.Code, f => f.Intensity)
				: null,
			WarningAreas = null,
			IsWarning = isWarning,
		};

		return new(AxisEewMessageKind.Eew, eew, eventId, reportTime);
	}

	// アプリ内の時刻は日本標準時の壁時計で統一しているため、マシンのタイムゾーンに依存しないよう明示的に変換する
	private static DateTime ToJstDateTime(DateTimeOffset value)
		=> value.ToOffset(TimeSpan.FromHours(9)).DateTime;

	private static int ResolveDepth(string? depth)
	{
		if (string.IsNullOrWhiteSpace(depth))
			return -1;

		var value = depth;
		if (value.EndsWith("km", StringComparison.OrdinalIgnoreCase))
			value = value[..^2];

		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : -1;
	}

	private static float? ResolveMagnitude(string? magnitude)
		=> float.TryParse(magnitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;

	private static JmaIntensity ResolveIntensity(string? intensity)
		=> string.IsNullOrWhiteSpace(intensity) ? JmaIntensity.Unknown : intensity.ToJmaIntensity();

	private static JmaIntensity ResolveForecastIntensity(EewIntensity? intensity)
	{
		if (intensity == null)
			return JmaIntensity.Unknown;
		if (string.Equals(intensity.To, "over", StringComparison.OrdinalIgnoreCase))
			return ResolveIntensity(intensity.From);

		var to = ResolveIntensity(intensity.To);
		return to != JmaIntensity.Unknown ? to : ResolveIntensity(intensity.From);
	}
}
