using System;

namespace KyoshinEewViewer.Notification;

public class TrayMenuItem(string text, Action onClicked)
{
	public Action OnClicked { get; } = onClicked;
	public string Text { get; } = text;
}
