using Prism.Events;
using System;

namespace KyoshinEewViewer.Events
{
	public class NetworkTimeSynced : PubSubEvent<DateTime>
	{
	}
}