using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using Microsoft.Extensions.Logging;
using Splat;
using System;

namespace SlackBot
{
	public partial class App : Application
	{
		public static ThemeSelector? Selector { get; private set; }

		public override void Initialize() => AvaloniaXamlLoader.Load(this);

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
                Utils.OverrideVersion = "SlackBot";

				var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
				// 強制設定
				config.Logging.Enable = true;
				config.Map.AutoFocusAnimation = false;
				config.Update.SendCrashReport = false;
				config.KyoshinMonitor.UseExperimentalShakeDetect = true;
				LoggingAdapter.EnableConsoleLogger = true;

				Selector = ThemeSelector.Create(".");
				Selector.EnableThemes(this);
				Selector.ApplyTheme(config.Theme.WindowThemeName, config.Theme.IntensityThemeName);

				KyoshinEewViewer.App.MainWindow = desktop.MainWindow = new MainWindow();
				Console.CancelKeyPress += (s, e) =>
				{
					e.Cancel = true;
					Locator.Current.RequireService<ILogManager>().GetLogger<App>().LogInfo("キャンセルキーを検知しました。");
					Dispatcher.UIThread.InvokeAsync(() => desktop.MainWindow.Close());
				};
			}

			base.OnFrameworkInitializationCompleted();
		}

		public override void RegisterServices()
		{
			AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new CustomFontManagerImpl());
			Locator.CurrentMutable.RegisterLazySingleton(ConfigurationLoader.Load, typeof(KyoshinEewViewerConfiguration));
			Locator.CurrentMutable.RegisterLazySingleton(() => new SeriesController(), typeof(SeriesController));
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			LoggingAdapter.Setup(config);

			KyoshinEewViewer.App.SetupIoc(Locator.GetLocator());
			SplatRegistrations.SetupIOC(Locator.GetLocator());
			base.RegisterServices();
		}
	}
}
