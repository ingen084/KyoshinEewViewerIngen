using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// EQMonitor Backend の EventMessage JSON → C# の Eew レコードへの変換
/// </summary>
public static class EqMonitorEventMessageConverter
{
	public static Eew? ToEew(EqMonitorEventMessage msg, DateTime replayTime)
	{
		if (msg.IsCancel == true)
			return new Eew
			{
				Id = msg.EventId,
				Source = EewSource.Dmdata,
				DisplaySource = "EQMonitor リプレイ",
				ReceiveTime = replayTime,
				SerialNo = msg.SerialNo,
				IsFinal = false,
				IsCancelled = true,
				IsTrueCancelled = true,
				Hypocenter = null,
				IsWarning = msg.IsWarning ?? false,
			};

		DateTime occurrenceTime;
		if (!string.IsNullOrEmpty(msg.OriginTime) && DateTime.TryParse(msg.OriginTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ot))
			occurrenceTime = ot;
		else if (!string.IsNullOrEmpty(msg.ArrivalTime) && DateTime.TryParse(msg.ArrivalTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
			occurrenceTime = at;
		else
			occurrenceTime = replayTime;

		var intensityMap = msg.Regions
			.Select(r => (Code: int.TryParse(r.Code, out var c) ? c : -1, Intensity: ParseJmaIntensity(r.Intensity)))
			.Where(r => r.Code >= 0 && r.Intensity != JmaIntensity.Unknown)
			.GroupBy(r => r.Code)
			.ToDictionary(g => g.Key, g => g.Max(r => r.Intensity));

		return new Eew
		{
			Id = msg.EventId,
			Source = EewSource.Dmdata,
			DisplaySource = "EQMonitor リプレイ",
			ReceiveTime = replayTime,
			SerialNo = msg.SerialNo,
			IsFinal = msg.IsLastInfo ?? false,
			MaxIntensity = ParseJmaIntensity(msg.MaxIntensity),
			Hypocenter = msg.Hypocenter != null
				? new EewHypocenter
				{
					OccurrenceTime = occurrenceTime,
					Place = msg.Hypocenter.Name,
					Location = new Location((float)msg.Hypocenter.Latitude, (float)msg.Hypocenter.Longitude),
					Magnitude = msg.Magnitude.HasValue ? (float)msg.Magnitude.Value : null,
					Depth = msg.Hypocenter.Depth,
					IsTemporary = false,
				}
				: null,
			IntensityForecastMap = intensityMap.Count > 0 ? intensityMap : null,
			IsWarning = msg.IsWarning ?? false,
		};
	}

	private static JmaIntensity ParseJmaIntensity(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return JmaIntensity.Unknown;

		return value switch
		{
			"0" or "!0" => JmaIntensity.Int0,
			"1" or "!1" => JmaIntensity.Int1,
			"2" or "!2" => JmaIntensity.Int2,
			"3" or "!3" => JmaIntensity.Int3,
			"4" or "!4" => JmaIntensity.Int4,
			"5-" or "!5-" => JmaIntensity.Int5Lower,
			"5+" or "!5+" => JmaIntensity.Int5Upper,
			"6-" or "!6-" => JmaIntensity.Int6Lower,
			"6+" or "!6+" => JmaIntensity.Int6Upper,
			"7" or "!7" => JmaIntensity.Int7,
			_ => JmaIntensity.Unknown,
		};
	}
}
