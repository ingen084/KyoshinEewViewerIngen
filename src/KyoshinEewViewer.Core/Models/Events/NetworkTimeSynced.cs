using System;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class NetworkTimeSynced
	{
		public NetworkTimeSynced(DateTime syncedTime)
		{
			SyncedTime = syncedTime;
		}

		public DateTime SyncedTime { get; }
	}
}