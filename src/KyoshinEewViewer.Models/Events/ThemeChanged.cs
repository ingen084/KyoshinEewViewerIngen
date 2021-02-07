using Prism.Events;

namespace KyoshinEewViewer.Models.Events
{
	public class ThemeChanged : PubSubEvent<ThemeChanged.ChangedTheme>
	{
		public enum ChangedTheme
		{
			Window,
			Intensity,
		}
	}
}
