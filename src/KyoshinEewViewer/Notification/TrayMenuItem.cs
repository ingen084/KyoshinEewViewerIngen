using System;

namespace KyoshinEewViewer.Notification
{
	public class TrayMenuItem
	{
		private static uint TotalCount = 100;
		public uint Id { get; } = ++TotalCount;
		public Action OnClicked { get; private set; }
		public string Text { get; private set; }


		public TrayMenuItem(string text, Action onClicked)
		{
			Text = text;
			OnClicked = onClicked;
		}
	}
}
