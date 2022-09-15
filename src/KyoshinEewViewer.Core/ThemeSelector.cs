using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Core;

public class ThemeSelector : ReactiveObject
{
	private Theme? _selectedWindowTheme;
	private Theme? _selectedIntensityTheme;
	private IList<Theme>? _windowThemes;
	private IList<Theme>? _intensityThemes;
	//private IList<Window>? _windows;

	public Theme? SelectedWindowTheme
	{
		get => _selectedWindowTheme;
		set => this.RaiseAndSetIfChanged(ref _selectedWindowTheme, value);
	}
	public Theme? SelectedIntensityTheme
	{
		get => _selectedIntensityTheme;
		set => this.RaiseAndSetIfChanged(ref _selectedIntensityTheme, value);
	}

	public IList<Theme>? WindowThemes
	{
		get => _windowThemes;
		set => this.RaiseAndSetIfChanged(ref _windowThemes, value);
	}
	public IList<Theme>? IntensityThemes
	{
		get => _intensityThemes;
		set => this.RaiseAndSetIfChanged(ref _intensityThemes, value);
	}

	//public IList<Window>? Windows
	//{
	//	get => _windows;
	//	set => this.RaiseAndSetIfChanged(ref _windows, value);
	//}

	private ThemeSelector()
	{
	}

	public static ThemeSelector Create(string basePath) => new ThemeSelector()
	{
		WindowThemes = new ObservableCollection<Theme>(),
		IntensityThemes = new ObservableCollection<Theme>(),
		//Windows = new ObservableCollection<Window>()
	}.LoadThemes(basePath);

	private ThemeSelector LoadThemes(string path)
	{
		LoadDefaultThemes();

		try
		{
			if (Directory.Exists(Path.Combine(path, "Themes")))
				foreach (var file in Directory.EnumerateFiles(Path.Combine(path, "Themes"), "*.axaml"))
				{
					var theme = LoadTheme(file, ThemeType.Window);
					if (theme != null)
						_windowThemes?.Add(theme);
				}
			if (Directory.Exists(Path.Combine(path, "IntensityThemes")))
				foreach (var file in Directory.EnumerateFiles(Path.Combine(path, "IntensityThemes"), "*.axaml"))
				{
					var theme = LoadTheme(file, ThemeType.Intensity);
					if (theme != null)
						_intensityThemes?.Add(theme);
				}
		}
		catch (Exception)
		{
			// ignored
		}

		_selectedWindowTheme = _windowThemes?.FirstOrDefault();
		_selectedIntensityTheme = _intensityThemes?.FirstOrDefault();

		return this;
	}

	public virtual void LoadDefaultThemes()
	{
		_windowThemes?.Add(new Theme(ThemeType.Window, "Light", new StyleInclude(new Uri("resm:Styles?assembly=KyoshinEewViewer.Core"))
		{
			Source = new Uri("avares://KyoshinEewViewer.Core/Themes/Light.axaml")
		}, this));
		_windowThemes?.Add(new Theme(ThemeType.Window, "Dark", new StyleInclude(new Uri("resm:Styles?assembly=KyoshinEewViewer.Core"))
		{
			Source = new Uri("avares://KyoshinEewViewer.Core/Themes/Dark.axaml")
		}, this));
		_windowThemes?.Add(new Theme(ThemeType.Window, "Blue", new StyleInclude(new Uri("resm:Styles?assembly=KyoshinEewViewer.Core"))
		{
			Source = new Uri("avares://KyoshinEewViewer.Core/Themes/Blue.axaml")
		}, this));
		_windowThemes?.Add(new Theme(ThemeType.Window, "Quarog", new StyleInclude(new Uri("resm:Styles?assembly=KyoshinEewViewer.Core"))
		{
			Source = new Uri("avares://KyoshinEewViewer.Core/Themes/Quarog.axaml")
		}, this));

		_intensityThemes?.Add(new Theme(ThemeType.Intensity, "Standard", new StyleInclude(new Uri("resm:Styles?assembly=KyoshinEewViewer.Core"))
		{
			Source = new Uri("avares://KyoshinEewViewer.Core/IntensityThemes/Standard.axaml")
		}, this));
		_intensityThemes?.Add(new Theme(ThemeType.Intensity, "Quarog", new StyleInclude(new Uri("resm:Styles?assembly=KyoshinEewViewer.Core"))
		{
			Source = new Uri("avares://KyoshinEewViewer.Core/IntensityThemes/Quarog.axaml")
		}, this));
		_intensityThemes?.Add(new Theme(ThemeType.Intensity, "Vivid", new StyleInclude(new Uri("resm:Styles?assembly=KyoshinEewViewer.Core"))
		{
			Source = new Uri("avares://KyoshinEewViewer.Core/IntensityThemes/Vivid.axaml")
		}, this));
	}

	public Theme LoadTheme(string file, ThemeType type)
	{
		var name = Path.GetFileNameWithoutExtension(file);
		var xaml = File.ReadAllText(file);
		var style = AvaloniaRuntimeXamlLoader.Parse<IStyle>(xaml);
		return new Theme(type, name, style, this);
	}

	public void EnableThemes(Application window)
	{
		//IDisposable? disposable = null;
		//IDisposable? disposable2 = null;

		if (_selectedIntensityTheme != null && _selectedIntensityTheme.Style != null)
		{
			if (window.Styles.FirstOrDefault(s => s is IntensityTheme) is IntensityTheme theme)
				window.Styles.Remove(theme);
			window.Styles.Insert(0, _selectedIntensityTheme.Style);
		}
		if (_selectedWindowTheme != null && _selectedWindowTheme.Style != null)
		{
			if (window.Styles.FirstOrDefault(s => s is KyoshinEewViewerTheme) is KyoshinEewViewerTheme theme)
				window.Styles.Remove(theme);
			window.Styles.Insert(0, _selectedWindowTheme.Style);
			if (AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>() is FluentAvaloniaTheme faTheme)
			{
				var isDark = true;
				if (window.TryFindResource("IsDarkTheme", out var isDarkRaw) && isDarkRaw is bool isd)
					isDark = isd;
				faTheme.RequestedTheme = isDark ? FluentAvaloniaTheme.DarkModeString : FluentAvaloniaTheme.LightModeString;
			}
		}

		this.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
		{
			if (x != null && x.Style != null)
			{
				window.Styles[0] = x.Style;
				if (AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>() is FluentAvaloniaTheme faTheme)
				{
					var isDark = true;
					if (window.TryFindResource("IsDarkTheme", out var isDarkRaw) && isDarkRaw is bool isd)
						isDark = isd;
					faTheme.RequestedTheme = isDark ? FluentAvaloniaTheme.DarkModeString : FluentAvaloniaTheme.LightModeString;
				}
			}
		});
		this.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null).Subscribe(x =>
		{
			if (x != null && x.Style != null)
				window.Styles[1] = x.Style;
		});
		//window.Opened += (sender, e) =>
		//{
		//	if (_windows != null)
		//	{
		//		_windows.Add(window);
		//		disposable = this.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
		//		{
		//			if (x != null && x.Style != null)
		//				window.Styles[0] = x.Style;
		//		});
		//		disposable2 = this.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null).Subscribe(x =>
		//		{
		//			if (x != null && x.Style != null)
		//				window.Styles[1] = x.Style;
		//		});
		//	}
		//};

		//window.Closing += (sender, e) =>
		//{
		//	disposable?.Dispose();
		//	disposable2?.Dispose();
		//	if (_windows != null)
		//		_windows.Remove(window);
		//};
	}

	public void ApplyWindowTheme(Theme theme)
	{
		if (theme != null)
			SelectedWindowTheme = theme;
	}
	public void ApplyIntensityTheme(Theme theme)
	{
		if (theme != null)
			SelectedIntensityTheme = theme;
	}

	public void ApplyWindowTheme(string themeName)
	{
		if (WindowThemes?.FirstOrDefault(t => t.Name == themeName) is Theme theme)
			SelectedWindowTheme = theme;
	}
	public void ApplyIntensityTheme(string themeName)
	{
		if (IntensityThemes?.FirstOrDefault(t => t.Name == themeName) is Theme theme)
			SelectedIntensityTheme = theme;
	}

	public void ApplyTheme(Theme window, Theme intensity)
	{
		ApplyWindowTheme(window);
		ApplyIntensityTheme(intensity);
	}
	public void ApplyTheme(string window, string intensity)
	{
		ApplyWindowTheme(window);
		ApplyIntensityTheme(intensity);
	}
}
