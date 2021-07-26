using ReactiveUI;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class DisplayWarningMessageUpdated
	{
		public DisplayWarningMessageUpdated(string message)
		{
			Message = message;
		}

		public string Message { get; }

		public static void SendWarningMessage(string message)
			=> MessageBus.Current.SendMessage(new DisplayWarningMessageUpdated(message));
	}
}
