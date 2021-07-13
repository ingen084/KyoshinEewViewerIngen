using Avalonia;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class MapNavigationRequested
	{
		public MapNavigationRequested(Rect? bound)
		{
			Bound = bound;
		}
		public Rect? Bound { get; set; }
	}
}
