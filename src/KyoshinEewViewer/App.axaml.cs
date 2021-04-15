using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace KyoshinEewViewer
{
	public class App : Application
	{
		public static ThemeSelector? Selector { get; private set; }
		public static MainWindow? MainWindow { get; private set; }

		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				Selector = ThemeSelector.Create(".");
				Selector.EnableThemes(this);
				ConfigurationService.Load();
				Selector.ApplyTheme(ConfigurationService.Default.Theme.WindowThemeName, ConfigurationService.Default.Theme.IntensityThemeName);

				desktop.MainWindow = MainWindow = new MainWindow
				{
					DataContext = new MainWindowViewModel(),
				};
				Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
					.Subscribe(x =>
					{
						ConfigurationService.Default.Theme.IntensityThemeName = x?.Name ?? "Standard";
						FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow);
					});
				Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
					.Subscribe(x => ConfigurationService.Default.Theme.WindowThemeName = x?.Name ?? "Light");

				desktop.Exit += (s, e) =>
				{
					MessageBus.Current.SendMessage(new ApplicationClosing());
					ConfigurationService.Save();
				};
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
