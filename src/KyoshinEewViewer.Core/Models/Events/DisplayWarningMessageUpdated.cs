using ReactiveUI;

namespace KyoshinEewViewer.Core.Models.Events;

public class DisplayWarningMessageUpdated(string message)
{
	public string Message { get; } = message;

	public static void SendWarningMessage(string message)
		=> MessageBus.Current.SendMessage(new DisplayWarningMessageUpdated(message));
}
