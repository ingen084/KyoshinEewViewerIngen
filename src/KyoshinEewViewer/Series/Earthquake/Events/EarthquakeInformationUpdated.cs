namespace KyoshinEewViewer.Series.Earthquake.Events;

public class EarthquakeInformationUpdated(Models.EarthquakeEvent earthquake)
{
	public Models.EarthquakeEvent Earthquake { get; } = earthquake;
}
