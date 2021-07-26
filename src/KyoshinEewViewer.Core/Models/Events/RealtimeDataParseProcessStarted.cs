using System;

namespace KyoshinEewViewer.Core.Models.Events
{
    public class RealtimeDataParseProcessStarted
    {
		public RealtimeDataParseProcessStarted(DateTime startedTimerTime)
		{
			StartedTimerTime = startedTimerTime;
		}

		public DateTime StartedTimerTime { get; }
    }
}
