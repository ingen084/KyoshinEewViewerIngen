using System;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class EewUpdated
	{
		public EewUpdated(DateTime time, Eew[] eews)
		{
			Time = time;
			Eews = eews;
		}

		public DateTime Time { get; }
		public Eew[] Eews { get; }
	}
}
