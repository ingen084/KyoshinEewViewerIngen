using KyoshinEewViewer.Map;
using KyoshinMonitorLib;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.Typhoon;

public static class CoordinateConverter
{
	private static readonly Regex CoordinateRegex = new(@"([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)/", RegexOptions.Compiled);
	public static Location? GetLocation(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = CoordinateRegex.Match(value);

		if (!float.TryParse(match?.Groups[1]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lat) ||
			!float.TryParse(match?.Groups[3]?.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lng))
			return null;

		return new Location(lat, lng);
	}

	public static Direction? GetDirection(string? value) => value switch
	{
		"北" => Direction.North,
		"南" => Direction.South,
		"東" => Direction.East,
		"西" => Direction.West,
		"北西" => Direction.Northwest,
		"北東" => Direction.Northeast,
		"南西" => Direction.Southwest,
		"南東" => Direction.Southeast,
		_ => null,
	};

	private const double EatrhRadius = 6371;
	private const double Radian = Math.PI / 180;
	private const double Degree = 180 / Math.PI;

	/// <summary>
	/// 地理座標を指定した方角に指定した距離移動させる
	/// </summary>
	// author: m-nohira
	public static Location MoveTo(this Location baseLoc, double degree, double distance)
	{
		if (baseLoc == null)
			throw new ArgumentNullException(nameof(baseLoc));
		if (distance <= 0)
			return new Location(baseLoc.Latitude, baseLoc.Longitude);

		var cLatRad = baseLoc.Latitude * Radian;

		var gammaRad = distance / 1000 / EatrhRadius;
		var invertCLatRad = (Math.PI / 2) - cLatRad;

		var cosInvertCRad = Math.Cos(invertCLatRad);
		var cosGammaRad = Math.Cos(gammaRad);
		var sinInvertCRad = Math.Sin(invertCLatRad);
		var sinGammaRad = Math.Sin(gammaRad);

		//球面三角形における正弦余弦定理使用
		var rad = degree * Radian;
		var cosInvDistLat = (cosInvertCRad * cosGammaRad) + (sinInvertCRad * sinGammaRad * Math.Cos(rad));
		var sinDLon = sinGammaRad * Math.Sin(rad) / Math.Sin(Math.Acos(cosInvDistLat));

		var lat = ((Math.PI / 2) - Math.Acos(cosInvDistLat)) * Degree;
		var lon = baseLoc.Longitude + Math.Asin(sinDLon) * Degree;
		return new Location((float)lat, (float)lon);
	}

	/// <summary>
	/// 2点間の地理座標の距離を返す
	/// </summary>
	public static double DistanceTo(this Location c1, Location c2)
	{
		var radLat1 = c1.Latitude * Radian;
		var radLat2 = c2.Latitude * Radian;
		var sinDegLat = Math.Sin((c2.Latitude - c1.Latitude) * Radian / 2);
		var sinDegLng = Math.Sin((c2.Longitude - c1.Longitude) * Radian / 2);
		var a = sinDegLat * sinDegLat + Math.Cos(radLat1) * Math.Cos(radLat2) * sinDegLng * sinDegLng;
		var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
		return EatrhRadius * c;
	}
	/// <summary>
	/// 2点の地理座標間の角度を返す
	/// </summary>
	public static double InitialBearingTo(this Location c1, Location c2)
	{
		var radLat1 = c1.Latitude * Radian;
		var radLat2 = c2.Latitude * Radian;

		var deltaLng = (c2.Longitude - c1.Longitude) * Radian;

		var y = Math.Sin(deltaLng) * Math.Cos(radLat2);
		var x = Math.Cos(radLat1) * Math.Sin(radLat2) - Math.Sin(radLat1) * Math.Cos(radLat2) * Math.Cos(deltaLng);
		return Math.Atan2(y, x) * Degree;
	}
}
