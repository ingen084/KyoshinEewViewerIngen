using System;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public class EarthquakeInformationFragmentProcessException : Exception
{
	public EarthquakeInformationFragmentProcessException(string message) : base(message)
	{
	}
	public EarthquakeInformationFragmentProcessException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
