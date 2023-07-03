using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Core;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using static KyoshinEewViewer.MapEditor.NativeMethods;

namespace KyoshinEewViewer.MapEditor;
public partial class App : Application
{
	public static ThemeSelector? Selector { get; private set; }

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

			desktop.MainWindow = new MainWindow();

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
		}

		base.OnFrameworkInitializationCompleted();
	}
}
