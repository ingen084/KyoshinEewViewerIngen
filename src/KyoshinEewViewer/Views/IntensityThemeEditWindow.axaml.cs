using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using System;
using System.IO;
using System.Text.Json;
using Path = System.IO.Path;
using System.ComponentModel;
using R3;

namespace KyoshinEewViewer.Views;

public partial class IntensityThemeEditWindow : Window
{
	private IDisposable? _themeSubscription;

	public IntensityThemeEditWindow()
	{
		InitializeComponent();

		rollbackButton.Click += async (_, _) =>
		{
			if (IntensityTheme == null)
				return;
			var result = await new FAContentDialog
			{
				Title = "復元する",
				Content = IntensityTheme.Meta.Type == ThemeType.ExternalFile ? "ファイルからテーマを読み込み直しますか？" : "編集中のテーマの変更を破棄しますか？",
				PrimaryButtonText = "はい",
				SecondaryButtonText = "いいえ",
			}.ShowAsync(this);

			if (result == FAContentDialogResult.Primary)
			{
				AssignTheme(IntensityTheme);
				IsSaved = true;
			}
		};

		saveButton.Click += async (_, _) =>
		{
			if (IntensityTheme == null || DataContext is not IntensityTheme theme || KyoshinEewViewerApp.Selector?.IntensityThemes is not { } intensityThemes)
				return;

			if (IntensityTheme.Meta.Type == ThemeType.BuiltIn)
			{
				var result = await new FAContentDialog
				{
					Title = "組み込みテーマの保存",
					Content = $"組み込みテーマは変更できないため、外部テーマとして保存します。\n{theme.Name}.json として保存します。ファイル名に使用できない文字が含まれていないか確認してください。",
					PrimaryButtonText = "はい",
					SecondaryButtonText = "いいえ",
				}.ShowAsync(this);

				if (result != FAContentDialogResult.Primary)
					return;

				try
				{
					var intensityThemesDir = Path.Combine(PlatformDirectories.ApplicationData, "IntensityThemes");
					PlatformDirectories.EnsureDirectoryExists(intensityThemesDir);
					var path = Path.Combine(intensityThemesDir, $"{theme.Name}.json");
					File.WriteAllText(path, JsonSerializer.Serialize(theme));
					IntensityTheme = new ThemeSelector.IntensityTheme(new(ThemeType.ExternalFile, Path.GetFileName(path)), theme, theme.CreateResourceDictionary());
					intensityThemes.Add(IntensityTheme);
				}
				catch (Exception ex)
				{
					await new FAContentDialog
					{
						Title = "保存に失敗",
						Content = $"テーマの保存に失敗しました: {ex.Message}",
						PrimaryButtonText = "OK",
					}.ShowAsync(this);
				}
				return;
			}

			if (IntensityTheme.Meta.Type == ThemeType.ExternalFile)
			{
				var result = await new FAContentDialog
				{
					Title = "外部テーマの保存",
					Content = $"{IntensityTheme.Meta.Identifier} にテーマを上書き保存しますか？",
					PrimaryButtonText = "はい",
					SecondaryButtonText = "いいえ",
				}.ShowAsync(this);
				if (result != FAContentDialogResult.Primary)
					return;
				try
				{
					var intensityThemesDir = Path.Combine(PlatformDirectories.ApplicationData, "IntensityThemes");
					PlatformDirectories.EnsureDirectoryExists(intensityThemesDir);
					File.WriteAllText(Path.Combine(intensityThemesDir, IntensityTheme.Meta.Identifier), JsonSerializer.Serialize(theme));
					var newTheme = new ThemeSelector.IntensityTheme(new(ThemeType.ExternalFile, IntensityTheme.Meta.Identifier), theme, theme.CreateResourceDictionary());
					var index = intensityThemes.IndexOf(IntensityTheme);
					intensityThemes.RemoveAt(index);
					intensityThemes.Insert(index, newTheme);
					IntensityTheme = newTheme;
				}
				catch (Exception ex)
				{
					await new FAContentDialog
					{
						Title = "保存に失敗",
						Content = $"テーマの保存に失敗しました: {ex.Message}",
						PrimaryButtonText = "OK",
					}.ShowAsync(this);
				}
				return;
			}
		};
	}


	public static readonly DirectProperty<IntensityThemeEditWindow, ThemeSelector.IntensityTheme?> IntensityThemeProperty =
		AvaloniaProperty.RegisterDirect<IntensityThemeEditWindow, ThemeSelector.IntensityTheme?>(nameof(IntensityTheme),
			o => o.IntensityTheme,
			(o, v) => o.IntensityTheme = v);

	private ThemeSelector.IntensityTheme? _intensityTheme = null;
	public ThemeSelector.IntensityTheme? IntensityTheme
	{
		get => _intensityTheme;
		set {
			SetAndRaise(IntensityThemeProperty, ref _intensityTheme, value);
			if (value == null)
				return;
			AssignTheme(value);
			themeDetailText.Text = value.Meta.DisplayName;
		}
	}

	private void AssignTheme(ThemeSelector.IntensityTheme theme)
	{
		_themeSubscription?.Dispose();
		var cloned = theme.Theme.Clone();
		DataContext = cloned;
		// R3 の Debounce は dotnet/reactive の Throttle 相当
		_themeSubscription = Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
				h => (_, e) => h(e),
				h => cloned.PropertyChanged += h,
				h => cloned.PropertyChanged -= h)
			.Debounce(TimeSpan.FromMilliseconds(100))
			.Subscribe(_ =>
			{
				UpdateTheme(cloned);
				IsSaved = false;
			});
		UpdateTheme(cloned);
		IsSaved = true;
	}

	private void UpdateTheme(IntensityTheme? c)
	{
		if (c == null || KyoshinEewViewerApp.Selector == null)
			return;

		Dispatcher.UIThread.Post(() =>
		{
			c = c.Clone();
			KyoshinEewViewerApp.Selector.SelectedIntensityTheme = new(new(ThemeType.Temporary, "編集中のテーマ"), c, c.CreateResourceDictionary());
		});
	}

	private bool IsSaved { get; set; }
	protected override async void OnClosing(WindowClosingEventArgs e)
	{
		base.OnClosing(e);

		if (IntensityTheme == null || KyoshinEewViewerApp.Selector == null)
			return;

		if (IsSaved)
		{
			Dispatcher.UIThread.Post(() =>
			{
				KyoshinEewViewerApp.Selector.SelectedIntensityTheme = IntensityTheme;
			});
			return;
		}

		e.Cancel = true;
		var result = await new FAContentDialog
		{
			Title = "テーマの変更を破棄",
			Content = "ウィンドウを閉じて編集中のテーマの変更を破棄しますか？",
			PrimaryButtonText = "はい",
			SecondaryButtonText = "いいえ",
		}.ShowAsync(this);
		if (result == FAContentDialogResult.Primary)
		{
			IsSaved = true;
			Close();
		}
	}
}
