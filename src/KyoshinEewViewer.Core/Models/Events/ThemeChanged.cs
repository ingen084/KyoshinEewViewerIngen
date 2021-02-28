namespace KyoshinEewViewer.Core.Models.Events
{
	public class ThemeChanged
	{
		public ChangedTheme ChangedType { get; }

		public ThemeChanged(ChangedTheme changedType)
		{
			ChangedType = changedType;
		}

		public enum ChangedTheme
		{
			Window,
			Intensity,
		}
	}
}
