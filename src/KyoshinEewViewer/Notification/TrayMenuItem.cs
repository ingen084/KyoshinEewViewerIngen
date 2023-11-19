using System;

namespace KyoshinEewViewer.Notification;

public class TrayMenuItem(string text, Action onClicked)
{
	private static uint _totalCount = 100;
	public uint Id { get; } = ++_totalCount;
	public Action OnClicked { get; private set; } = onClicked;
	public string Text { get; private set; } = text;
}
