using KyoshinEewViewer.Models;
using Prism.Events;

namespace KyoshinEewViewer.Models.Events
{
	public class EarthquakeUpdated : PubSubEvent<Earthquake>
	{
	}
}
