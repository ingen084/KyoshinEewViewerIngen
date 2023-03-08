using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Views.SettingPages;
public partial class KyoshinMonitorMapPage : UserControl
{
	public KyoshinMonitorMapPage()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
