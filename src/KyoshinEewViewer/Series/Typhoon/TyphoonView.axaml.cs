using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Series.Typhoon;

public partial class TyphoonView : UserControl
{
	public TyphoonView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
