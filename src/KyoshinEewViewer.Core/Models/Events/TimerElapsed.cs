using System;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class TimerElapsed
	{
		public DateTime Time { get; }

		public TimerElapsed(DateTime time)
		{
			Time = time;
		}
	}
}
