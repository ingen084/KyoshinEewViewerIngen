using System;

namespace KyoshinEewViewer.Series.Earthquake.Services;

public class EarthquakeWatchException(string? message) : Exception(message)
{
}
