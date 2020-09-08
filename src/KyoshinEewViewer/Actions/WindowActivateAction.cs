using System.Windows;
using System.Windows.Interactivity;

namespace KyoshinEewViewer.Actions
{
	public class WindowActivateAction : TriggerAction<Window>
	{
		protected override void Invoke(object parameter)
		{
			AssociatedObject.Show();
			AssociatedObject.Activate();
		}
	}
}
