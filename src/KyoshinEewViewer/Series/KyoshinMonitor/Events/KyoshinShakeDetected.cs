using KyoshinEewViewer.Core.Models;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Events;

public class KyoshinShakeDetected
{
	public KyoshinEvent Event { get; }
	public bool IsLevelUp { get; }

	public KyoshinShakeDetected(KyoshinEvent @event, bool isLevelUp)
	{
		Event = @event;
		IsLevelUp = isLevelUp;
	}
}
