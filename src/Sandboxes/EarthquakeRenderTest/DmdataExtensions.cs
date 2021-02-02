using DmdataSharp.ApiResponses.Parameters;
using KyoshinMonitorLib;

namespace EarthquakeRenderTest
{
	public static class DmdataExtensions
	{
		public static Location GetLocation(this EarthquakeStationParameterResponse.Item item)
		{
			if (!float.TryParse(item.Latitude, out var lat) || !float.TryParse(item.Longitude, out var lng))
				return null;
			return new Location(lat, lng);
		}
		public static Location GetLocation(this TsunamiStationParameterResponse.Item item)
		{
			if (!float.TryParse(item.Latitude, out var lat) || !float.TryParse(item.Longitude, out var lng))
				return null;
			return new Location(lat, lng);
		}
	}
}
