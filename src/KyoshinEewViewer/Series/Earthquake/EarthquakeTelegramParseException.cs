using System;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeTelegramParseException(string? message) : Exception(message)
{
}
