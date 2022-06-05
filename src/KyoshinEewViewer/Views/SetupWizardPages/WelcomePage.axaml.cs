using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace KyoshinEewViewer.Views.SetupWizardPages;
public partial class WelcomePage : UserControl
{
	public WelcomePage()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		if (new Random().NextDouble() < .05) // 5%
			this.FindControl<TextBlock>("titleText")!.Text = Properties.Resources.SetupWizardWelcomePageTitleAlt;
	}
}
