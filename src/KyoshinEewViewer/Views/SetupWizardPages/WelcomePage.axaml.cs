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

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		if (new Random().NextDouble() < .05) // 5%
			TitleText.Text = Properties.Resources.SetupWizardWelcomePageTitleAlt;
	}
}
