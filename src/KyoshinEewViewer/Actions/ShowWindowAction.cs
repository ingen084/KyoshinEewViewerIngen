using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace KyoshinEewViewer.Actions
{
	public abstract class ShowWindowAction<T> : TriggerAction<Window> where T : Window, new()
	{
		private T window;

		protected override void Invoke(object parameter)
		{
			if (window == null)
			{
				window = new T { Owner = AssociatedObject };
				window.Closed += (s, e) => window = null;
				window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			window?.Show();
		}
	}
}