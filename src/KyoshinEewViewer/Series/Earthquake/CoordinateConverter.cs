using KyoshinMonitorLib;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.Earthquake;

public static class CoordinateConverter
{
	private static readonly Regex CoordinateRegex = new(@"([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)?", RegexOptions.Compiled);
	public static int? GetDepth(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex.Match(value);

		if (double.TryParse(match?.Groups[5]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var depth))
			return (int)-depth / 1000;
		return null;
	}

	public static Location? GetLocation(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex.Match(value);

		if (!float.TryParse(match?.Groups[1]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lat) || !float.TryParse(match?.Groups[3]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lng))
			return null;

		return new Location(lat, lng);
	}
}
