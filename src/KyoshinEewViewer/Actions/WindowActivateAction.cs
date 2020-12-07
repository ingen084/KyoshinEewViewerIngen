using Microsoft.Xaml.Behaviors;
using System.Windows;

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
