using Avalonia;

namespace KyoshinEewViewer.Core.Models.Events;

public class MapNavigationRequest(Rect? bound, Rect? mustBound = null)
{
	public Rect? Bound { get; set; } = bound;
	public Rect? MustBound { get; set; } = mustBound;
}
