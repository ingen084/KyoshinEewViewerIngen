﻿using KyoshinMonitorLib;
using System.Text.RegularExpressions;

namespace EarthquakeRenderTest
{
	public static class CoordinateConverter
	{
		private readonly static Regex CoordinateRegex = new Regex(@"([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)(-\d+(\.\d)?)?", RegexOptions.Compiled);
		public static int? GetDepth(string value)
		{
			var match = CoordinateRegex.Match(value);

			if (int.TryParse(match?.Groups[5]?.Value, out var depth))
				return -depth / 1000;
			return null;
		}

		public static Location GetLocation(string value)
		{
			var match = CoordinateRegex.Match(value);

			if (!float.TryParse(match?.Groups[1]?.Value, out var lat) || !float.TryParse(match?.Groups[3]?.Value, out var lng))
				return null;

			return new Location(lat, lng);
		}
	}
}
