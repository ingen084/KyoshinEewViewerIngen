using KyoshinEewViewer.Core.Models;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Events;

public class KyoshinShakeDetected(KyoshinEvent @event, bool isLevelUp)
{
	public KyoshinEvent Event { get; } = @event;
	public bool IsLevelUp { get; } = isLevelUp;
}
