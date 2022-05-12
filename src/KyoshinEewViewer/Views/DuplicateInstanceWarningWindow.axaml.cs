using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KyoshinEewViewer.Views;
public partial class DuplicateInstanceWarningWindow : Window
{
	public bool IsContinue { get; private set; }

	public DuplicateInstanceWarningWindow()
	{
		InitializeComponent();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);

		this.FindControl<Button>("continueButton")!.Tapped += (s, e) => { IsContinue = true; Close(); };
		this.FindControl<Button>("exitButton")!.Tapped += (s, e) => Close();
	}
}
