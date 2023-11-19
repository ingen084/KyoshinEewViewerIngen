using ReactiveUI;
using System;

namespace KyoshinEewViewer.Core.Models.Events;

public class KyoshinMonitorReplayRequested(string? basePath, DateTime? time)
{
	public string? BasePath { get; } = basePath;
	public DateTime? Time { get; } = time;

	public static void Request(string? basePath, DateTime? time)
		=> MessageBus.Current.SendMessage(new KyoshinMonitorReplayRequested(basePath, time));
}
