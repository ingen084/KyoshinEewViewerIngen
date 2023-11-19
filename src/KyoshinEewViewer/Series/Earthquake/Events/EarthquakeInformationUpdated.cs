namespace KyoshinEewViewer.Series.Earthquake.Events;

public class EarthquakeInformationUpdated(Models.Earthquake earthquake)
{
	public Models.Earthquake Earthquake { get; } = earthquake;
}
