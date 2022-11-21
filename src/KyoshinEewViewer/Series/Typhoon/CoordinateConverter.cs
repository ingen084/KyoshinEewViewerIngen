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

		if (!float.TryParse(match?.Groups[1]?.Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var lat) || !float.TryParse(match?.Groups[3]?.Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var lng))
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

	private const double EATRH_RADIUS = 6371;
	private const double RADIAN = Math.PI / 180;
	private const double DEGREE = 180 / Math.PI;

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

		var c_lat_rad = baseLoc.Latitude * RADIAN;

		var gamma_rad = distance / 1000 / EATRH_RADIUS;
		var invert_c_lat_rad = (Math.PI / 2) - c_lat_rad;

		var cos_invert_c_rad = Math.Cos(invert_c_lat_rad);
		var cos_gamma_rad = Math.Cos(gamma_rad);
		var sin_invert_c_rad = Math.Sin(invert_c_lat_rad);
		var sin_gamma_rad = Math.Sin(gamma_rad);

		//球面三角形における正弦余弦定理使用
		var rad = degree * RADIAN;
		var cos_inv_dist_lat = (cos_invert_c_rad * cos_gamma_rad) + (sin_invert_c_rad * sin_gamma_rad * Math.Cos(rad));
		var sin_d_lon = sin_gamma_rad * Math.Sin(rad) / Math.Sin(Math.Acos(cos_inv_dist_lat));

		var lat = ((Math.PI / 2) - Math.Acos(cos_inv_dist_lat)) * DEGREE;
		var lon = baseLoc.Longitude + Math.Asin(sin_d_lon) * DEGREE;
		return new Location((float)lat, (float)lon);
	}

	/// <summary>
	/// 2点間の地理座標の距離を返す
	/// </summary>
	public static double DistanceTo(this Location c1, Location c2)
	{
		var radLat1 = c1.Latitude * RADIAN;
		var radLat2 = c2.Latitude * RADIAN;
		var sinDegLat = Math.Sin((c2.Latitude - c1.Latitude) * RADIAN / 2);
		var sinDegLng = Math.Sin((c2.Longitude - c1.Longitude) * RADIAN / 2);
		var a = sinDegLat * sinDegLat + Math.Cos(radLat1) * Math.Cos(radLat2) * sinDegLng * sinDegLng;
		var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
		return EATRH_RADIUS * c;
	}
	/// <summary>
	/// 2点の地理座標間の角度を返す
	/// </summary>
	public static double InitialBearingTo(this Location c1, Location c2)
	{
		var radLat1 = c1.Latitude * RADIAN;
		var radLat2 = c2.Latitude * RADIAN;

		var deltaLng = (c2.Longitude - c1.Longitude) * RADIAN;

		var y = Math.Sin(deltaLng) * Math.Cos(radLat2);
		var x = Math.Cos(radLat1) * Math.Sin(radLat2) - Math.Sin(radLat1) * Math.Cos(radLat2) * Math.Cos(deltaLng);
		return Math.Atan2(y, x) * DEGREE;
	}
}
