    using System;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class DelayedTimeElapsed
	{
		public DelayedTimeElapsed(DateTime time)
		{
			Time = time;
		}

		public DateTime Time { get; }
	}
}