using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.UiTests.Infrastructure;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using KyoshinEewViewer.Views.SetupWizardPages;

namespace KyoshinEewViewer.UiTests;

public sealed class WizardUiTests
{
	[AvaloniaFact]
	public void 地震情報の選択をクリックすると設定へ反映され他の選択は変わらない()
	{
		var config = new KyoshinEewViewerConfiguration();
		var model = new SetupWizardWindowViewModel(config);
		var view = new SelectSeriesPage { DataContext = model };
		var window = new Window { Width = 800, Height = 900, Content = view };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var checkbox = Find(view, "地震情報");
			var before = model.IsEarthquakeEnabled;
			var other = model.IsTsunamiEnabled;
			Assert.Equal(before, checkbox.IsChecked);
			var point = checkbox.TranslatePoint(new Point(12, checkbox.Bounds.Height / 2), window)!.Value;
			window.MouseDown(point, MouseButton.Left);
			window.MouseUp(point, MouseButton.Left);
			Assert.Equal(!before, model.IsEarthquakeEnabled);
			Assert.Equal(!before, config.SeriesEnable["earthquake"]);
			Assert.Equal(other, model.IsTsunamiEnabled);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void SignalNow連携をキーボードで変更すると入れ子の設定へ反映される()
	{
		var config = new KyoshinEewViewerConfiguration();
		config.Eew.EnableSignalNowProfessional = false;
		var view = new SelectSeriesPage { DataContext = new SetupWizardWindowViewModel(config) };
		var window = new Window { Width = 800, Height = 900, Content = view };
		try
		{
			window.Show();
			var checkbox = Find(view, "Signal Now Professional 連携機能を利用する");
			Assert.True(checkbox.Focus());
			window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
			window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
			Assert.True(config.Eew.EnableSignalNowProfessional);
			config.Eew.EnableSignalNowProfessional = false;
			Dispatcher.UIThread.RunJobs();
			Assert.False(checkbox.IsChecked);
		}
		finally { window.Close(); }
	}

	private static readonly Type[] PageOrder = [typeof(WelcomePage), typeof(SelectThemePage), typeof(SelectSeriesPage), typeof(DmdataPromotion), typeof(EpiloguePage)];

	[AvaloniaFact]
	public void 進むと戻るでページと操作できるボタンが切り替わり_最後のページでは起動になる()
		=> RunWizard((window, wizard) =>
		{
			AssertPage(wizard, 0);
			for (var i = 1; i < PageOrder.Length; i++)
			{
				Ui.Click(window, wizard.Next);
				AssertPage(wizard, i);
			}
			Ui.Click(window, wizard.Before);
			AssertPage(wizard, PageOrder.Length - 2);

			var continued = 0;
			window.Continued += () => continued++;
			Ui.Click(window, wizard.Skip);
			Assert.Equal(1, continued);
		});

	[AvaloniaFact]
	public void キーボードでもページ送りと起動ができる()
		=> RunWizard((window, wizard) =>
		{
			Assert.True(wizard.Next.Focus());
			Ui.PressKey(window, PhysicalKey.Enter);
			AssertPage(wizard, 1);
			Ui.PressKey(window, PhysicalKey.Space);
			AssertPage(wizard, 2);

			Assert.True(wizard.Before.Focus());
			Ui.PressKey(window, PhysicalKey.Space);
			AssertPage(wizard, 1);

			var continued = 0;
			window.Continued += () => continued++;
			Assert.True(wizard.Skip.Focus());
			Ui.PressKey(window, PhysicalKey.Enter);
			Assert.Equal(1, continued);
		});

	private sealed record WizardControls(Button Before, Button Next, Button Skip, TextBlock SkipText, TextBlock PageGuide, ContentControl Content);

	private static void RunWizard(Action<SetupWizardWindow, WizardControls> test)
	{
		// テーマ選択ページは起動時に作られたテーマ一覧を参照する。
		var previousSelector = KyoshinEewViewerApp.Selector;
		KyoshinEewViewerApp.Selector = ThemeSelector.Create(null);
		var window = new SetupWizardWindow { DataContext = new SetupWizardWindowViewModel(new KyoshinEewViewerConfiguration()) };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			test(window, new(
				window.FindControl<Button>("BeforeButton")!,
				window.FindControl<Button>("NextButton")!,
				window.FindControl<Button>("SkipButton")!,
				window.FindControl<TextBlock>("SkipButtonText")!,
				window.FindControl<TextBlock>("PageGuide")!,
				window.FindControl<ContentControl>("ContentControl")!));
		}
		finally
		{
			window.Close();
			KyoshinEewViewerApp.Selector = previousSelector;
		}
	}

	private static void AssertPage(WizardControls wizard, int index)
	{
		Dispatcher.UIThread.RunJobs();
		Assert.IsType(PageOrder[index], wizard.Content.Content);
		Assert.Equal($"{index + 1}/{PageOrder.Length}", wizard.PageGuide.Text);
		Assert.Equal(index > 0, wizard.Before.IsEnabled);
		Assert.Equal(index < PageOrder.Length - 1, wizard.Next.IsEnabled);
		Assert.Equal(index < PageOrder.Length - 1 ? "スキップして起動" : "起動", wizard.SkipText.Text);
	}

	private static CheckBox Find(Control view, string content)
		=> view.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, content));
}
