using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.ViewModels;
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

	private static CheckBox Find(Control view, string content)
		=> view.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, content));
}
