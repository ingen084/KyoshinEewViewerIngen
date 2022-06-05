using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Services;
using System;
using System.Collections.Generic;

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
		SkipButton = this.FindControl<Button>("skipButton")!;
		SkipButton.Tapped += (s, e) => Continued?.Invoke();
		SkipButtonText = this.FindControl<TextBlock>("skipButtonText")!;
		BeforeButton = this.FindControl<Button>("beforeButton")!;
		BeforeButton.Tapped += (s, e) => { Index--; UpdatePage(); };
		NextButton = this.FindControl<Button>("nextButton")!;
		NextButton.Tapped += (s, e) => { Index++; UpdatePage(); };

		ContentControl = this.FindControl<ContentControl>("contentControl")!;
		PageGuide = this.FindControl<TextBlock>("pageGuide")!;

		UpdatePage();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	ContentControl ContentControl { get; }
	Button SkipButton { get; }
	TextBlock SkipButtonText { get; }
	Button BeforeButton { get; }
	Button NextButton { get; }
	TextBlock PageGuide { get; }

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void UpdatePage()
	{
		if (Index == 0)
		{
			BeforeButton.IsEnabled = false;
			NextButton.IsEnabled = true;
			SkipButtonText.Text = Properties.Resources.SetupWizardSkipAndRun;
		}
		else if (Index >= Pages.Length - 1)
		{
			BeforeButton.IsEnabled = true;
			NextButton.IsEnabled = false;
			SkipButtonText.Text = Properties.Resources.SetupWizardRun;
		}
		else
		{
			BeforeButton.IsEnabled = true;
			NextButton.IsEnabled = true;
			SkipButtonText.Text = Properties.Resources.SetupWizardSkipAndRun;
		}
		SkipButtonText.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
		ContentControl.Content = Pages[Index];
		PageGuide.Text = $"{Index + 1}/{Pages.Length}";
	}
}
