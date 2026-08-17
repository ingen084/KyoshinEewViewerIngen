using Avalonia.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;

namespace KyoshinEewViewer.Views;

public partial class SettingWindow : Window
{
	public SettingWindow()
	{
		InitializeComponent();
		Closed += (s, e) =>
		{
			var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();
			ConfigurationLoader.Save(config);
		};
	}
}
