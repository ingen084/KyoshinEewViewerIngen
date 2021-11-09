using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Services;

namespace KyoshinEewViewer.Views;

public class SettingWindow : Window
{
	public SettingWindow()
	{
		InitializeComponent();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);

		Closed += (s, e) => ConfigurationService.Save();
	}
}
