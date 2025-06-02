using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;

namespace KyoshinEewViewer.Core;

public class ThemeSelector : ReactiveObject
{
	public record WindowTheme(ThemeMeta Meta, Models.WindowTheme Theme, ResourceDictionary Style);
	public record IntensityTheme(ThemeMeta Meta, Models.IntensityTheme Theme, ResourceDictionary Style);

	private WindowTheme? _selectedWindowTheme;
	private IntensityTheme? _selectedIntensityTheme;
	private IList<WindowTheme>? _windowThemes;
	private IList<IntensityTheme>? _intensityThemes;

	public WindowTheme? SelectedWindowTheme
	{
		get => _selectedWindowTheme;
		set => this.RaiseAndSetIfChanged(ref _selectedWindowTheme, value);
	}
	public IntensityTheme? SelectedIntensityTheme
	{
		get => _selectedIntensityTheme;
		set => this.RaiseAndSetIfChanged(ref _selectedIntensityTheme, value);
	}

	public IList<WindowTheme>? WindowThemes
	{
		get => _windowThemes;
		set => this.RaiseAndSetIfChanged(ref _windowThemes, value);
	}
	public IList<IntensityTheme>? IntensityThemes
	{
		get => _intensityThemes;
		set => this.RaiseAndSetIfChanged(ref _intensityThemes, value);
	}

	private ThemeSelector()
	{
	}

	public static ThemeSelector Create(string? basePath) => new ThemeSelector()
	{
		WindowThemes = new ObservableCollection<WindowTheme>(),
		IntensityThemes = new ObservableCollection<IntensityTheme>(),
	}.LoadThemes(basePath);

	private ThemeSelector LoadThemes(string? path)
	{
		LoadDefaultThemes();

		if (path != null)
			try
			{
				if (Directory.Exists(Path.Combine(path, "Themes")))
					foreach (var file in Directory.EnumerateFiles(Path.Combine(path, "Themes"), "*.json"))
					{
						try
						{
							if (JsonSerializer.Deserialize(File.ReadAllText(file), KyoshinEewViewerSerializerContext.Default.WindowTheme) is { } theme)
								_windowThemes?.Add(new(new(ThemeType.ExternalFile, Path.GetFileName(file)), theme, theme.CreateResourceDictionary()));
						}
						catch (Exception e)
						{
							// ignored
						}
					}
				if (Directory.Exists(Path.Combine(path, "IntensityThemes")))
					foreach (var file in Directory.EnumerateFiles(Path.Combine(path, "IntensityThemes"), "*.json"))
					{
						try
						{
							if (JsonSerializer.Deserialize(File.ReadAllText(file), KyoshinEewViewerSerializerContext.Default.IntensityTheme) is { } theme)
								_intensityThemes?.Add(new(new(ThemeType.ExternalFile, Path.GetFileName(file)), theme, theme.CreateResourceDictionary()));
						}
						catch (Exception)
						{
							// ignored
						}
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
		foreach (var t in Models.WindowTheme.DefaultThemes)
			_windowThemes?.Add(new(new(ThemeType.BuiltIn, t.Name), t, t.CreateResourceDictionary()));

		foreach (var t in Models.IntensityTheme.DefaultThemes)
			_intensityThemes?.Add(new(new(ThemeType.BuiltIn, t.Name), t, t.CreateResourceDictionary()));
	}

	public void EnableThemes(Application window)
	{
		if (_selectedIntensityTheme?.Style != null)
		{
			if (window.Styles.FirstOrDefault(s => s is KyoshinEewViewerIntensityTheme) is not KyoshinEewViewerIntensityTheme theme)
			{
				theme = [];
				window.Styles.Insert(0, theme);
			}
			theme.Resources = _selectedIntensityTheme.Style;
		}
		if (_selectedWindowTheme?.Style != null)
		{
			if (window.Styles.FirstOrDefault(s => s is KyoshinEewViewerWindowTheme) is not KyoshinEewViewerWindowTheme theme)
			{
				theme = [];
				window.Styles.Insert(0, theme);
			}
			theme.Resources = _selectedWindowTheme.Style;
			window.RequestedThemeVariant = _selectedWindowTheme.Theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
		}

		this.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
		{
			if (x?.Style != null)
			{
				if (window.Styles.FirstOrDefault(s => s is KyoshinEewViewerWindowTheme) is not KyoshinEewViewerWindowTheme theme)
				{
					theme = [];
					window.Styles.Insert(0, theme);
				}
				theme.Resources = x.Style;
				window.RequestedThemeVariant = x.Theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
			}
		});
		this.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null).Subscribe(x =>
		{
			if (x?.Style != null)
			{
				if (window.Styles.FirstOrDefault(s => s is KyoshinEewViewerIntensityTheme) is not KyoshinEewViewerIntensityTheme theme)
				{
					theme = [];
					window.Styles.Insert(0, theme);
				}
				theme.Resources = x.Style;
			}
		});
	}

	public void ApplyTheme(ThemeMeta window, ThemeMeta intensity)
	{
		if (WindowThemes?.FirstOrDefault(t => t.Meta == window) is { } w)
			SelectedWindowTheme = w;
		if (IntensityThemes?.FirstOrDefault(t => t.Meta == intensity) is { } i)
			SelectedIntensityTheme = i;
	}
}

public class KyoshinEewViewerWindowTheme : Styles, IResourceProvider
{
	public KyoshinEewViewerWindowTheme()
	{
		Resources = Models.WindowTheme.Dark.CreateResourceDictionary();
	}
}
public class KyoshinEewViewerIntensityTheme : Styles, IResourceProvider
{
	public KyoshinEewViewerIntensityTheme()
	{
		Resources = Models.IntensityTheme.Standard.CreateResourceDictionary();
	}
}
