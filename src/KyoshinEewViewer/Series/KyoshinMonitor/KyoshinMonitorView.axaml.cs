using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Series.KyoshinMonitor
{
	public class KyoshinMonitorView : UserControl
	{
		public KyoshinMonitorView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
