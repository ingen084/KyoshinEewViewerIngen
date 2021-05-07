using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Views
{
	public partial class UpdateWindow : Window
	{
		public UpdateWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
