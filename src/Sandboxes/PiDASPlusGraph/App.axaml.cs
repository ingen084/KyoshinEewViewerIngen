using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.CustomControl;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using static PiDASPlusGraph.NativeMethods;

namespace PiDASPlusGraph;

public class App : Application
{
	private const string ConfigFileName = "ppg-config.json";

	public static ThemeSelector? Selector { get; private set; }
	public static PpgConfig Config { get; private set; } = new();

	private static bool _configLoadedFromApplicationData = true;

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		Config = LoadConfig();

		Selector = ThemeSelector.Create(PlatformDirectories.ApplicationData);
		Selector.EnableThemes(this);
		Selector.ApplyTheme(Config.WindowTheme, Config.IntensityTheme);

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
			desktop.MainWindow.Opened += (_, _) => ApplyDwmAttributes(desktop.MainWindow);

			Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
			{
				Config.WindowTheme = x!.Meta;
				Dispatcher.UIThread.Post(() => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow!));

				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					return;

				foreach (var window in desktop.Windows)
					ApplyDwmAttributes(window);
			});
			Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null).Subscribe(x =>
			{
				Config.IntensityTheme = x!.Meta;
				Dispatcher.UIThread.Post(() => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow!));
			});

			desktop.Exit += (_, _) => SaveConfig();
		}

		base.OnFrameworkInitializationCompleted();
	}

	private static PpgConfig LoadConfig()
	{
		try
		{
			if (File.Exists(ConfigFileName))
			{
				var config = JsonSerializer.Deserialize(File.ReadAllText(ConfigFileName), PpgConfigSerializerContext.Default.PpgConfig);
				if (config != null)
				{
					_configLoadedFromApplicationData = false;
					return config;
				}
			}

			var appDataPath = Path.Combine(PlatformDirectories.ApplicationData, ConfigFileName);
			if (File.Exists(appDataPath))
			{
				var config = JsonSerializer.Deserialize(File.ReadAllText(appDataPath), PpgConfigSerializerContext.Default.PpgConfig);
				if (config != null)
				{
					_configLoadedFromApplicationData = true;
					return config;
				}
			}
		}
		catch
		{
		}
		return new PpgConfig();
	}

	public static void SaveConfig()
	{
		try
		{
			string fileName;
			if (_configLoadedFromApplicationData)
			{
				PlatformDirectories.EnsureDirectoryExists(PlatformDirectories.ApplicationData);
				fileName = Path.Combine(PlatformDirectories.ApplicationData, ConfigFileName);
			}
			else
				fileName = ConfigFileName;

			File.WriteAllText(fileName, JsonSerializer.Serialize(Config, PpgConfigSerializerContext.Default.PpgConfig));
		}
		catch
		{
		}
	}

	public static void ApplyDwmAttributes(Window window)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || window.TryGetPlatformHandle()?.Handle is not { } handle)
			return;

		var isDarkTheme = (bool)(window.FindResource("IsDarkTheme") ?? false);
		var useDarkMode = isDarkTheme ? 1 : 0;
		DwmSetWindowAttribute(
			handle,
			Dwmwindowattribute.DwmwaUseImmersiveDarkMode,
			ref useDarkMode,
			Marshal.SizeOf(useDarkMode));

		if (window.FindResource("TitleBackgroundColor") is Avalonia.Media.Color color)
		{
			var intColor = color.R | color.G << 8 | color.B << 16;
			DwmSetWindowAttribute(
				handle,
				Dwmwindowattribute.DwmwaCaptionColor,
				ref intColor,
				Marshal.SizeOf(intColor));
		}
	}
}
