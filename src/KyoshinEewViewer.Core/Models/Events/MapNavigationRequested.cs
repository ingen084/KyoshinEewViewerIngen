using Avalonia;

namespace KyoshinEewViewer.Core.Models.Events;

public class MapNavigationRequested
{
	public MapNavigationRequested(Rect? bound, Rect? mustBound = null)
	{
		Bound = bound;
		MustBound = mustBound;
	}
	public Rect? Bound { get; set; }
	public Rect? MustBound { get; set; }
}
