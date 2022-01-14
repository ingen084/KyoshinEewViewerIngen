using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Splat;
using System;
using System.Diagnostics;
using System.Reactive.Linq;

namespace KyoshinEewViewer;

public class App : Application
{
	public static ThemeSelector? Selector { get; private set; }
	public static MainWindow? MainWindow { get; private set; }

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);
			ConfigurationService.Load();

			Trace.Listeners.Add(new LoggingTraceListener());

			Selector.ApplyTheme(ConfigurationService.Current.Theme.WindowThemeName, ConfigurationService.Current.Theme.IntensityThemeName);

			desktop.MainWindow = MainWindow = new MainWindow
			{
				DataContext = new MainWindowViewModel(),
			};
			Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
				.Subscribe(x =>
				{
					ConfigurationService.Current.Theme.IntensityThemeName = x?.Name ?? "Standard";
					FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow);
				});
			Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => ConfigurationService.Current.Theme.WindowThemeName = x?.Name ?? "Light");

			desktop.Exit += (s, e) =>
			{
				MessageBus.Current.SendMessage(new ApplicationClosing());
				ConfigurationService.Save();
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// override RegisterServices register custom service
	/// </summary>
	public override void RegisterServices()
	{
		AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new CustomFontManagerImpl());
		Locator.CurrentMutable.RegisterLazySingleton(() => new NotificationService(), typeof(NotificationService));
		base.RegisterServices();
	}

	public class LoggingTraceListener : TraceListener
	{
		private Microsoft.Extensions.Logging.ILogger Logger { get; }

		public LoggingTraceListener()
		{
			Logger = LoggingService.CreateLogger(this);
		}

		public override void Write(string? message)
			=> Logger.LogTrace("writed: {message}", message);

		public override void WriteLine(string? message)
			=> Logger.LogTrace("line writed: {message}", message);
	}
}
