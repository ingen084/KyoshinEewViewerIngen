using DmdataSharp.ApiResponses.V2.Parameters;
using KyoshinMonitorLib;
using System.Globalization;

namespace KyoshinEewViewer.Series.Earthquake;

public static class DmdataExtensions
{
	public static Location? GetLocation(this EarthquakeStationParameterResponse.Item item)
	{
		if (!float.TryParse(item.Latitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lat) ||
			!float.TryParse(item.Longitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lng))
			return null;
		return new Location(lat, lng);
	}
	public static Location? GetLocation(this TsunamiStationParameterResponse.Item item)
	{
		if (!float.TryParse(item.Latitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lat) ||
			!float.TryParse(item.Longitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lng))
			return null;
		return new Location(lat, lng);
	}
}
