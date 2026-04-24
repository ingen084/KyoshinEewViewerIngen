using KyoshinMonitorLib;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.Earthquake;

public static partial class CoordinateConverter
{
	[GeneratedRegex(@"([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)?", RegexOptions.Compiled)]
	private static partial Regex CoordinateRegex();
	public static int? GetDepth(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex().Match(value);

		if (double.TryParse(match?.Groups[5]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var depth))
			return (int)-depth / 1000;
		return null;
	}

	public static Location? GetLocation(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex().Match(value);

		if (!float.TryParse(match?.Groups[1]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lat) || !float.TryParse(match?.Groups[3]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lng))
			return null;

		return new Location(lat, lng);
	}

	/// <summary>
	/// 度分形式の座標をパースする (例: +3930+14059-10000/ → 39°30' 140°59')
	/// </summary>
	public static Location? GetLocationFromDegreeMinute(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex().Match(value);
		if (!match.Success)
			return null;

		if (!TryParseDegreeMinute(match.Groups[1].Value, 2, out var lat) ||
			!TryParseDegreeMinute(match.Groups[3].Value, 3, out var lng))
			return null;

		return new Location(lat, lng);
	}

	private static bool TryParseDegreeMinute(string value, int degreeDigits, out float result)
	{
		result = 0;
		if (string.IsNullOrEmpty(value))
			return false;

		var sign = 1;
		if (value[0] == '+' || value[0] == '-')
		{
			sign = value[0] == '-' ? -1 : 1;
			value = value[1..];
		}
		if (value.Length < degreeDigits)
			return false;

		if (!int.TryParse(value[..degreeDigits], NumberStyles.None, CultureInfo.InvariantCulture, out var degrees))
			return false;
		if (!float.TryParse(value[degreeDigits..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var minutes))
			return false;

		result = sign * (degrees + minutes / 60f);
		return true;
	}
}
