using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services;
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

				Selector = ThemeSelector.Create(".");
				Selector.EnableThemes(this);
				Selector.ApplyTheme(ConfigurationService.Current.Theme.WindowThemeName, ConfigurationService.Current.Theme.IntensityThemeName);

				KyoshinEewViewer.App.MainWindow = desktop.MainWindow = new MainWindow();
				Console.CancelKeyPress += (s, e) =>
				{
					e.Cancel = true;
					LoggingService.CreateLogger<App>().LogInformation("キャンセルキーを検知しました。");
					Dispatcher.UIThread.InvokeAsync(() => desktop.MainWindow.Close());
				};
            }

            base.OnFrameworkInitializationCompleted();
        }

		public override void RegisterServices()
		{
			AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new CustomFontManagerImpl());
			Locator.CurrentMutable.RegisterLazySingleton(() => new TelegramProvideService(), typeof(TelegramProvideService));
			base.RegisterServices();
		}
	}
}
