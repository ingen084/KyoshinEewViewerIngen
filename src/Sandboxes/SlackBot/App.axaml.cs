using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
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
					Locator.Current.RequireService<ILoggerFactory>().CreateLogger<App>().LogInformation("キャンセルキーを検知しました。");
					Dispatcher.UIThread.InvokeAsync(() => desktop.MainWindow.Close());
				};
			}

			base.OnFrameworkInitializationCompleted();
		}

		public override void RegisterServices()
		{
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new CustomFontManagerImpl());
			LoggingAdapter.Setup(config);

			SplatRegistrations.SetupIOC(Locator.GetLocator());
			base.RegisterServices();
		}
	}
}
