using System;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeTelegramParseException : Exception
{
	public EarthquakeTelegramParseException(string? message) : base(message)
	{
	}
}
