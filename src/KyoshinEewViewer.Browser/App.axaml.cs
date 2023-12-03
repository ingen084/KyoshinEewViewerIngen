using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Browser;

public class App : Application
{
	private static Control? _mainView;
	public static Control? MainView
	{
		get => _mainView;
		set {
			_mainView = value;
			KyoshinEewViewerApp.TopLevelControl = TopLevel.GetTopLevel(value);
		}
	}

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		Console.WriteLine("RI: " + RuntimeInformation.RuntimeIdentifier);

		if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			KyoshinEewViewerApp.Selector = ThemeSelector.Create(null);
			KyoshinEewViewerApp.Selector.EnableThemes(this);

			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();

			singleViewPlatform.MainView = MainView = new MainView
			{
				DataContext = Locator.Current.RequireService<MainViewModel>(),
			};

			KyoshinEewViewerApp.Selector.ApplyTheme(config.Theme.WindowThemeName, config.Theme.IntensityThemeName);
			KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
				.Subscribe(x =>
				{
					config.Theme.IntensityThemeName = x?.Name ?? "Standard";
					if (singleViewPlatform.MainView != null)
						FixedObjectRenderer.UpdateIntensityPaintCache(singleViewPlatform.MainView);
				});
			KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
			{
				config.Theme.WindowThemeName = x?.Name ?? "Light";
				FixedObjectRenderer.UpdateIntensityPaintCache(singleViewPlatform.MainView);
			});
		}

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// override RegisterServices register custom service
	/// </summary>
	public override void RegisterServices()
	{
		Locator.CurrentMutable.RegisterLazySingleton(ConfigurationLoader.Load, typeof(KyoshinEewViewerConfiguration));
		Locator.CurrentMutable.RegisterLazySingleton(() => new SeriesController(), typeof(SeriesController));
		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		LoggingAdapter.Setup(config);

		SetupIOC(Locator.GetLocator());
		base.RegisterServices();
	}


	public static void SetupIOC(IDependencyResolver resolver)
	{
		KyoshinEewViewerApp.SetupIOC(resolver);
		SplatRegistrations.SetupIOC(resolver);
	}
}
