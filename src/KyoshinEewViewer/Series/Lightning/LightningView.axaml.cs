using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Series.Lightning
{
	public partial class LightningView : UserControl
	{
		public LightningView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
