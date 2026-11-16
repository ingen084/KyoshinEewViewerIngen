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
using System;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;

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
		KyoshinEewViewerApp.Application = this;

		if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			KyoshinEewViewerApp.Selector = ThemeSelector.Create(null);
			KyoshinEewViewerApp.Selector.EnableThemes(this);

			var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();

			singleViewPlatform.MainView = MainView = new MainView
			{
				DataContext = ServiceLocator.Current.RequireService<MainViewModel>(),
			};

			KyoshinEewViewerApp.Selector.ApplyTheme(config.Theme.WindowTheme, config.Theme.IntensityTheme);
			KyoshinEewViewerApp.Selector.ObservePropertyChanged(x => x.SelectedIntensityTheme)
				.SubscribeLoggingErrors(x =>
				{
					if (x == null) return;
					config.Theme.IntensityTheme = x.Meta;
					FixedObjectRenderer.UpdateIntensityPaintCache(this);
				});
			KyoshinEewViewerApp.Selector.ObservePropertyChanged(x => x.SelectedWindowTheme).SubscribeLoggingErrors(x =>
			{
				if (x == null) return;
				config.Theme.WindowTheme = x.Meta;
				FixedObjectRenderer.UpdateIntensityPaintCache(this);
			});
		}

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// override RegisterServices register custom service
	/// </summary>
	public override void RegisterServices()
	{
		var services = new ServiceCollection();
		services.SetupConfigurationAndLogging();
		services.AddKyoshinEewViewer();

		ServiceLocator.SetProvider(services.BuildServiceProvider());
		base.RegisterServices();
	}


}
