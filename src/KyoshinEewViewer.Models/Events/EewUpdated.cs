using Prism.Events;
using System;

namespace KyoshinEewViewer.Models.Events
{
	public class EewUpdated : PubSubEvent<EewUpdated>
	{
		public DateTime Time { get; set; }
		public Eew[] Eews { get; set; }
	}
}
