using Avalonia.Controls;
using KyoshinEewViewer.Services;

namespace KyoshinEewViewer.Views;

public partial class SettingWindow : Window
{
	public SettingWindow()
	{
		InitializeComponent();
		Closed += (s, e) => ConfigurationService.Save();
	}
}
