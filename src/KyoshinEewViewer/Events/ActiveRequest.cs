using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Series;

namespace KyoshinEewViewer.Events;

public record class ActiveRequest(SeriesBase Series)
{
	public static void Send(SeriesBase series)
		=> StrongReferenceMessenger.Default.Send(new ActiveRequest(series));
}
