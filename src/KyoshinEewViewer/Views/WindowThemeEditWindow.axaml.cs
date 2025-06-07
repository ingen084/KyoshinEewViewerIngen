using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Text.Json;
using Path = System.IO.Path;

namespace KyoshinEewViewer.Views;

public partial class WindowThemeEditWindow : Window
{
	private IDisposable? _themeSubscription;

	public WindowThemeEditWindow()
	{
		InitializeComponent();

		rollbackButton.Click += async (_, _) =>
		{
			if (WindowTheme == null)
				return;
			var result = await new ContentDialog
			{
				Title = "復元する",
				Content = WindowTheme.Meta.Type == ThemeType.ExternalFile ? "ファイルからテーマを読み込み直しますか？" : "編集中のテーマの変更を破棄しますか？",
				PrimaryButtonText = "はい",
				SecondaryButtonText = "いいえ",
			}.ShowAsync(this);

			if (result == ContentDialogResult.Primary)
			{
				AssignTheme(WindowTheme);
				IsSaved = true;
			}
		};

		saveButton.Click += async (_, _) =>
		{
			if (WindowTheme == null || DataContext is not WindowTheme theme || KyoshinEewViewerApp.Selector?.WindowThemes is not { } windowThemes)
				return;

			if (WindowTheme.Meta.Type == ThemeType.BuiltIn)
			{
				var result = await new ContentDialog
				{
					Title = "組み込みテーマの保存",
					Content = $"組み込みテーマは変更できないため、外部テーマとして保存します。\nThemes/{theme.Name}.json として保存します。ファイル名に使用できない文字が含まれていないか確認してください。",
					PrimaryButtonText = "はい",
					SecondaryButtonText = "いいえ",
				}.ShowAsync(this);

				if (result != ContentDialogResult.Primary)
					return;

				try
				{
					if (!Directory.Exists("Themes"))
						Directory.CreateDirectory("Themes");
					var path = Path.Combine("Themes", $"{theme.Name}.json");
					File.WriteAllText(path, JsonSerializer.Serialize(theme));
					WindowTheme = new ThemeSelector.WindowTheme(new(ThemeType.ExternalFile, Path.GetFileName(path)), theme, theme.CreateResourceDictionary());
					windowThemes.Add(WindowTheme);
				}
				catch (Exception ex)
				{
					await new ContentDialog
					{
						Title = "保存に失敗",
						Content = $"テーマの保存に失敗しました: {ex.Message}",
						PrimaryButtonText = "OK",
					}.ShowAsync(this);
				}
				return;
			}

			if (WindowTheme.Meta.Type == ThemeType.ExternalFile)
			{
				var result = await new ContentDialog
				{
					Title = "外部テーマの保存",
					Content = $"Themes/{WindowTheme.Meta.Identifier} にテーマを上書き保存しますか？",
					PrimaryButtonText = "はい",
					SecondaryButtonText = "いいえ",
				}.ShowAsync(this);
				if (result != ContentDialogResult.Primary)
					return;
				try
				{
					File.WriteAllText(Path.Combine("Themes", WindowTheme.Meta.Identifier), JsonSerializer.Serialize(theme));
					var newTheme = new ThemeSelector.WindowTheme(new(ThemeType.ExternalFile, WindowTheme.Meta.Identifier), theme, theme.CreateResourceDictionary());
					var index = windowThemes.IndexOf(WindowTheme);
					windowThemes.RemoveAt(index);
					windowThemes.Insert(index, newTheme);
					WindowTheme = newTheme;
				}
				catch (Exception ex)
				{
					await new ContentDialog
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


	public static readonly DirectProperty<WindowThemeEditWindow, ThemeSelector.WindowTheme?> WindowThemeProperty =
		AvaloniaProperty.RegisterDirect<WindowThemeEditWindow, ThemeSelector.WindowTheme?>(nameof(WindowTheme),
			o => o.WindowTheme,
			(o, v) => o.WindowTheme = v);

	private ThemeSelector.WindowTheme? _windowTheme = null;
	public ThemeSelector.WindowTheme? WindowTheme
	{
		get => _windowTheme;
		set {
			SetAndRaise(WindowThemeProperty, ref _windowTheme, value);
			if (value == null)
				return;
			AssignTheme(value);
			themeDetailText.Text = value.Meta.DisplayName;
		}
	}

	private void AssignTheme(ThemeSelector.WindowTheme theme)
	{
		_themeSubscription?.Dispose();
		var cloned = theme.Theme.Clone();
		DataContext = cloned;
		_themeSubscription = cloned.WhenAnyPropertyChanged()
			.Throttle(TimeSpan.FromMilliseconds(100))
			.Subscribe(c =>
			{
				UpdateTheme(c);
				IsSaved = false;
			});
		UpdateTheme(cloned);
		IsSaved = true;
	}

	private void UpdateTheme(WindowTheme? c)
	{
		if (c == null || KyoshinEewViewerApp.Selector == null)
			return;

		Dispatcher.UIThread.Post(() =>
		{
			c = c.Clone();
			KyoshinEewViewerApp.Selector.SelectedWindowTheme = new(new(ThemeType.Temporary, "編集中のテーマ"), c, c.CreateResourceDictionary());
		});
	}

	private bool IsSaved { get; set; }
	protected override async void OnClosing(WindowClosingEventArgs e)
	{
		base.OnClosing(e);

		if (WindowTheme == null || KyoshinEewViewerApp.Selector == null)
			return;

		if (IsSaved)
		{
			Dispatcher.UIThread.Post(() =>
			{
				KyoshinEewViewerApp.Selector.SelectedWindowTheme = WindowTheme;
			});
			return;
		}

		e.Cancel = true;
		var result = await new ContentDialog
		{
			Title = "テーマの変更を破棄",
			Content = "ウィンドウを閉じて編集中のテーマの変更を破棄しますか？",
			PrimaryButtonText = "はい",
			SecondaryButtonText = "いいえ",
		}.ShowAsync(this);
		if (result == ContentDialogResult.Primary)
		{
			IsSaved = true;
			Close();
		}
	}
}
