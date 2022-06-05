using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Views.SetupWizardPages;
public partial class SelectThemePage : UserControl
{
	public SelectThemePage()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
