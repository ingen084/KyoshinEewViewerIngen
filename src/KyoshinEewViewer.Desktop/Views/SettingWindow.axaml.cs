using Avalonia.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using Splat;

namespace KyoshinEewViewer.Desktop.Views;

public partial class SettingWindow : Window
{
	public SettingWindow()
	{
		InitializeComponent();
		Closed += (s, e) =>
		{
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			ConfigurationLoader.Save(config);
		};
	}
}
