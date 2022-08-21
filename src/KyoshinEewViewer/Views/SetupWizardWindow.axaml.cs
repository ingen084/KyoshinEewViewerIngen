using Avalonia.Controls;
using System;

namespace KyoshinEewViewer.Views;
public partial class SetupWizardWindow : Window
{
	public event Action? Continued;

	private int Index { get; set; }
	private UserControl[] Pages { get; } = new UserControl[]
	{
		new SetupWizardPages.WelcomePage(),
		new SetupWizardPages.SelectThemePage(),
		new SetupWizardPages.SelectSeriesPage(),
		new SetupWizardPages.DmdataPromotion(),
		new SetupWizardPages.EpiloguePage(),
	};

	public SetupWizardWindow()
	{
		InitializeComponent();

		skipButton.Tapped += (s, e) => Continued?.Invoke();
		beforeButton.Tapped += (s, e) => { Index--; UpdatePage(); };
		nextButton.Tapped += (s, e) => { Index++; UpdatePage(); };
		UpdatePage();
	}

	private void UpdatePage()
	{
		if (Index == 0)
		{
			beforeButton.IsEnabled = false;
			nextButton.IsEnabled = true;
			skipButtonText.Text = Properties.Resources.SetupWizardSkipAndRun;
		}
		else if (Index >= Pages.Length - 1)
		{
			beforeButton.IsEnabled = true;
			nextButton.IsEnabled = false;
			skipButtonText.Text = Properties.Resources.SetupWizardRun;
		}
		else
		{
			beforeButton.IsEnabled = true;
			nextButton.IsEnabled = true;
			skipButtonText.Text = Properties.Resources.SetupWizardSkipAndRun;
		}
		skipButtonText.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
		contentControl.Content = Pages[Index];
		pageGuide.Text = $"{Index + 1}/{Pages.Length}";
	}
}
