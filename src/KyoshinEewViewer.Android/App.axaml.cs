using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Android;

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
		// フォントリソースのURLメモ
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Regular.otf"
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Bold.otf"
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/FontAwesome6Free-Solid-900.otf"
		// "avares://FluentAvalonia/Fonts/FluentAvalonia.ttf"

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
			//KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
			//	.Subscribe(x =>
			//	{
			//		config.Theme.IntensityThemeName = x?.Name ?? "Standard";
			//		if (singleViewPlatform.MainView != null)
			//			FixedObjectRenderer.UpdateIntensityPaintCache(singleViewPlatform.MainView);
			//	});
			//KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
			//{
			//	config.Theme.WindowThemeName = x?.Name ?? "Light";
			//	FixedObjectRenderer.UpdateIntensityPaintCache(singleViewPlatform.MainView);
			//});
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

	public void OpenSettingsClicked(object sender, EventArgs args)
		=> MessageBus.Current.SendMessage(new ShowSettingWindowRequested());

	public static void SetupIOC(IDependencyResolver resolver)
	{
		KyoshinEewViewerApp.SetupIOC(resolver);
		SplatRegistrations.SetupIOC(resolver);
	}
}
