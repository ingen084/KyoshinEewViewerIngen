using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using R3;
using System;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;

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
		KyoshinEewViewerApp.Application = this;

		// フォントリソースのURLメモ
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Regular.otf"
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Bold.otf"
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/FontAwesome6Free-Solid-900.otf"
		// "avares://FluentAvalonia/Fonts/FluentAvalonia.ttf"
		if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			KyoshinEewViewerApp.Selector = ThemeSelector.Create(null);
			KyoshinEewViewerApp.Selector.EnableThemes(this);

			var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();

			singleViewPlatform.MainView = MainView = new MainView
			{
				DataContext = ServiceLocator.Current.RequireService<MainViewModel>(),
			};

			// NOTE: 旧バージョンからの移行
			if (config.Theme.WindowThemeName is string windowTheme)
			{
				config.Theme.WindowTheme = KyoshinEewViewerApp.Selector.WindowThemes?.FirstOrDefault(t => t.Meta.Identifier == windowTheme)?.Meta ?? config.Theme.WindowTheme;
				config.Theme.WindowThemeName = null; // 古い設定を削除
			}
			if (config.Theme.IntensityThemeName is string intensityTheme)
			{
				config.Theme.IntensityTheme = KyoshinEewViewerApp.Selector.IntensityThemes?.FirstOrDefault(t => t.Meta.Identifier == intensityTheme)?.Meta ?? config.Theme.IntensityTheme;
				config.Theme.IntensityThemeName = null; // 古い設定を削除
			}

			KyoshinEewViewerApp.Selector.ApplyTheme(config.Theme.WindowTheme, config.Theme.IntensityTheme);
			KyoshinEewViewerApp.Selector.ObservePropertyChanged(x => x.SelectedIntensityTheme)
				.Subscribe(x =>
				{
					if (x == null) return;
					config.Theme.IntensityTheme = x.Meta;
					Dispatcher.UIThread.Post(() => FixedObjectRenderer.UpdateIntensityPaintCache(singleViewPlatform.MainView));
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

	public void OpenSettingsClicked(object sender, EventArgs args)
		=> StrongReferenceMessenger.Default.Send(new ShowSettingWindowRequested());

}
