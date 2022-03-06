using System;

namespace KyoshinEewViewer.Series.Earthquake.Services;

public class EarthquakeWatchException : Exception
{
	public EarthquakeWatchException(string? message) : base(message)
	{
	}
}
