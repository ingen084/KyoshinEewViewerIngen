using KyoshinEewViewer.Series;
using ReactiveUI;

namespace KyoshinEewViewer.Events;

public record class ActiveRequest(SeriesBase Series)
{
	public static void Send(SeriesBase series)
		=> MessageBus.Current.SendMessage(new ActiveRequest(series));
}
