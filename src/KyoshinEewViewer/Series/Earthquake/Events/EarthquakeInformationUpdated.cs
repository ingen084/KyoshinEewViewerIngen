using KyoshinMonitorLib;

namespace KyoshinEewViewer.Series.Earthquake.Events;

public class EarthquakeInformationUpdated
{
	public Models.Earthquake Earthquake { get; }

	public EarthquakeInformationUpdated(Models.Earthquake earthquake)
	{
		Earthquake = earthquake;
	}
}
