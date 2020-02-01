using KyoshinEewViewer.Models;
using Prism.Events;

namespace KyoshinEewViewer.Events
{
	public class EarthquakeUpdated : PubSubEvent<Earthquake>
	{
	}
}
