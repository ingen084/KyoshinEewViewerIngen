using KyoshinMonitorLib;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public static class CoordinateConverter
{
	private static readonly Regex CoordinateRegex = new(@"([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)?", RegexOptions.Compiled);
	public static int? GetDepth(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex.Match(value);

		if (double.TryParse(match?.Groups[5]?.Value, out var depth))
			return (int)-depth / 1000;
		return null;
	}

	public static Location? GetLocation(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex.Match(value);

		if (!float.TryParse(match?.Groups[1]?.Value, out var lat) || !float.TryParse(match?.Groups[3]?.Value, out var lng))
			return null;

		return new Location(lat, lng);
	}
}
