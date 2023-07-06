using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using ReactiveUI;
using Splat;
using System;

namespace SlackBot
{
	public class App : Application
	{
		public static ThemeSelector? Selector { get; private set; }

		public override void Initialize() => AvaloniaXamlLoader.Load(this);

		public override void OnFrameworkInitializationCompleted()
		{
            Utils.OverrideVersion = "SlackBot";

			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			Selector.ApplyTheme(config.Theme.WindowThemeName, config.Theme.IntensityThemeName);
			Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
				.Subscribe(x =>
				{
					config.Theme.IntensityThemeName = x?.Name ?? "Standard";
					FixedObjectRenderer.UpdateIntensityPaintCache(desktop.Windows[0]);
				});

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
				KyoshinEewViewer.App.MainWindow = desktop.MainWindow = new MainWindow();

			base.OnFrameworkInitializationCompleted();
		}

		public override void RegisterServices()
		{
			Locator.CurrentMutable.RegisterLazySingleton(ConfigurationLoader.Load, typeof(KyoshinEewViewerConfiguration));
			Locator.CurrentMutable.RegisterLazySingleton(() => new SeriesController(), typeof(SeriesController));
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			// 強制設定
			config.Logging.Enable = true;
			config.Map.AutoFocusAnimation = false;
			config.Update.SendCrashReport = false;
			config.KyoshinMonitor.UseExperimentalShakeDetect = true;
			config.Earthquake.ShowHistory = false;
			LoggingAdapter.Setup(config);

			KyoshinEewViewer.App.SetupIoc(Locator.GetLocator());
			SplatRegistrations.SetupIOC(Locator.GetLocator());
			base.RegisterServices();
		}
	}
}
