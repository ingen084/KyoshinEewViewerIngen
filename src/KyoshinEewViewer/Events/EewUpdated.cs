using Prism.Events;
using System;

namespace KyoshinEewViewer.Events
{
	public class EewUpdated : PubSubEvent<EewUpdated>
	{
		public DateTime Time { get; set; }
		public Models.Eew[] Eews { get; set; }
	}
}
