using ReactiveUI;

namespace KyoshinEewViewer.Core.Models.Events;

public class ProcessJmaEqdbRequested
{
	public string Id { get; }

	private ProcessJmaEqdbRequested(string id)
	{
		Id = id;
	}

	public static void Request(string id)
		=> MessageBus.Current.SendMessage(new ProcessJmaEqdbRequested(id));
}
