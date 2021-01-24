using Prism.Events;
using System;

namespace KyoshinEewViewer.Models.Events
{
	public class NetworkTimeSynced : PubSubEvent<DateTime>
	{
	}
}