using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CustomRenderItemTest.ViewModels;
using CustomRenderItemTest.Views;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using static CustomRenderItemTest.NativeMethods;

namespace CustomRenderItemTest;

public class App : Application
{
	public static ThemeSelector? Selector { get; private set; }

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var splashWindow = new SplashWindow();
			splashWindow.Show();

			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);

			Dispatcher.UIThread.InvokeAsync(() =>
			{
				desktop.MainWindow = new MainWindow
				{
					DataContext = new MainWindowViewModel(),
				};
				Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
					.Subscribe(x => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow));
				Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
				{
					if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || desktop.MainWindow.TryGetPlatformHandle()?.Handle is not { } handle)
						return;
					
					Avalonia.Media.Color FindColorResource(string name)
						=> (Avalonia.Media.Color)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"マップリソース {name} が見つかりませんでした"));
					bool FindBoolResource(string name)
						=> (bool)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

					var isDarkTheme = FindBoolResource("IsDarkTheme");
					var useDarkMode = isDarkTheme ? 1 : 0;
					DwmSetWindowAttribute(
						handle,
						Dwmwindowattribute.DwmwaUseImmersiveDarkMode,
						ref useDarkMode,
						Marshal.SizeOf(useDarkMode));

					var color = FindColorResource("TitleBackgroundColor");
					var intColor = color.R | color.G << 8 | color.B << 16;
					DwmSetWindowAttribute(
						handle,
						Dwmwindowattribute.DwmwaCaptionColor,
						ref intColor,
						Marshal.SizeOf(intColor));
				});
				desktop.MainWindow.Show();
				desktop.MainWindow.Activate();
				splashWindow.Close();
			});

			desktop.Exit += (s, e) => MessageBus.Current.SendMessage(new ApplicationClosing());
		}
		else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			Selector = ThemeSelector.Create(null);
			Selector.EnableThemes(this);

			singleViewPlatform.MainView = new MainView
			{
				DataContext = new MainWindowViewModel()
			};

			Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null).Subscribe(x => FixedObjectRenderer.UpdateIntensityPaintCache(this));
			Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x => FixedObjectRenderer.UpdateIntensityPaintCache(this));
		}
		base.OnFrameworkInitializationCompleted();
	}
}
