using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Series.Earthquake
{
	public class EarthquakeView : UserControl
	{
		public EarthquakeView()
		{
			InitializeComponent();
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
	}
}
