using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;

namespace SlackBot
{
	public class App : Application
	{
		public override void Initialize() => AvaloniaXamlLoader.Load(this);

		public override void OnFrameworkInitializationCompleted()
		{
            Utils.OverrideVersion = "SlackBot";

			KyoshinEewViewerApp.Selector = ThemeSelector.Create(".");
			KyoshinEewViewerApp.Selector.EnableThemes(this);
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				KyoshinEewViewerApp.TopLevelControl = desktop.MainWindow = new MainWindow();
				KyoshinEewViewerApp.Selector.ApplyTheme(config.Theme.WindowThemeName, config.Theme.IntensityThemeName);
				KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
					.Subscribe(x =>
					{
						config.Theme.IntensityThemeName = x?.Name ?? "Standard";
						FixedObjectRenderer.UpdateIntensityPaintCache(this);
					});
				KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
				{
					config.Theme.WindowThemeName = x?.Name ?? "Light";
					FixedObjectRenderer.UpdateIntensityPaintCache(this);
				});
			}

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

			KyoshinEewViewerApp.SetupIOC(Locator.GetLocator());
			SplatRegistrations.SetupIOC(Locator.GetLocator());
			base.RegisterServices();
		}
	}
}
