using ReactiveUI;
using System;

namespace KyoshinEewViewer.Core.Models.Events;

public class KyoshinMonitorReplayRequested
{
	public KyoshinMonitorReplayRequested(string basePath, DateTime? time)
	{
		BasePath = basePath;
		Time = time;
	}

	public string BasePath { get; }
	public DateTime? Time { get; }

	public static void Request(string basePath, DateTime? time)
		=> MessageBus.Current.SendMessage(new KyoshinMonitorReplayRequested(basePath, time));
}
