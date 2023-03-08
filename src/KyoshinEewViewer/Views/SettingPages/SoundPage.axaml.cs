using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Views.SettingPages;
public partial class SoundPage : UserControl
{
	public SoundPage()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
