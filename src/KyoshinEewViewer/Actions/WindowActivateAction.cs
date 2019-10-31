using System;
using System.Windows;
using System.Windows.Interactivity;

namespace KyoshinEewViewer.Actions
{
	public class WindowActivateAction : TriggerAction<Window>
	{
		protected override void Invoke(object parameter)
		{
			AssociatedObject.Show();
			// なぜ最大化にしないともとに戻ってこないんだ…？
			AssociatedObject.WindowState = WindowState.Maximized;
			AssociatedObject.Activate();
		}
	}
}
