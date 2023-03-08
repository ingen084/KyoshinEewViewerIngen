using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CustomRenderItemTest.Views;
public partial class SplashWindow : Window
{
	public SplashWindow()
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
