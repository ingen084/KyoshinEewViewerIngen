using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.CustomControl;

/// <summary>
/// ContentDialog の結果
/// </summary>
public enum ContentDialogResult
{
	None,
	Primary,
	Secondary
}

/// <summary>
/// ContentDialog のデフォルトボタン
/// </summary>
public enum ContentDialogButton
{
	None,
	Primary,
	Secondary,
	Close
}

/// <summary>
/// TopLevel の OverlayLayer 上にモーダルダイアログを表示するコントロール
/// </summary>
public class ContentDialog : ContentControl
{
	public static readonly StyledProperty<object?> TitleProperty =
		AvaloniaProperty.Register<ContentDialog, object?>(nameof(Title));

	public static readonly StyledProperty<string?> PrimaryButtonTextProperty =
		AvaloniaProperty.Register<ContentDialog, string?>(nameof(PrimaryButtonText));

	public static readonly StyledProperty<string?> SecondaryButtonTextProperty =
		AvaloniaProperty.Register<ContentDialog, string?>(nameof(SecondaryButtonText));

	public static readonly StyledProperty<string?> CloseButtonTextProperty =
		AvaloniaProperty.Register<ContentDialog, string?>(nameof(CloseButtonText));

	public static readonly StyledProperty<ContentDialogButton> DefaultButtonProperty =
		AvaloniaProperty.Register<ContentDialog, ContentDialogButton>(nameof(DefaultButton));

	/// <summary>
	/// ダイアログのタイトル
	/// </summary>
	public object? Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <summary>
	/// プライマリボタンのテキスト
	/// </summary>
	public string? PrimaryButtonText
	{
		get => GetValue(PrimaryButtonTextProperty);
		set => SetValue(PrimaryButtonTextProperty, value);
	}

	/// <summary>
	/// セカンダリボタンのテキスト
	/// </summary>
	public string? SecondaryButtonText
	{
		get => GetValue(SecondaryButtonTextProperty);
		set => SetValue(SecondaryButtonTextProperty, value);
	}

	/// <summary>
	/// 閉じるボタンのテキスト
	/// </summary>
	public string? CloseButtonText
	{
		get => GetValue(CloseButtonTextProperty);
		set => SetValue(CloseButtonTextProperty, value);
	}

	/// <summary>
	/// デフォルトボタン
	/// </summary>
	public ContentDialogButton DefaultButton
	{
		get => GetValue(DefaultButtonProperty);
		set => SetValue(DefaultButtonProperty, value);
	}

	private TaskCompletionSource<ContentDialogResult>? _tcs;
	private Button? _primaryButton;
	private Button? _secondaryButton;
	private Button? _closeButton;
	private DialogHost? _host;
	private IInputElement? _lastFocus;

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		if (_primaryButton != null)
			_primaryButton.Click -= OnButtonClick;
		if (_secondaryButton != null)
			_secondaryButton.Click -= OnButtonClick;
		if (_closeButton != null)
			_closeButton.Click -= OnButtonClick;

		base.OnApplyTemplate(e);

		_primaryButton = e.NameScope.Find<Button>("PrimaryButton");
		_secondaryButton = e.NameScope.Find<Button>("SecondaryButton");
		_closeButton = e.NameScope.Find<Button>("CloseButton");

		if (_primaryButton != null)
			_primaryButton.Click += OnButtonClick;
		if (_secondaryButton != null)
			_secondaryButton.Click += OnButtonClick;
		if (_closeButton != null)
			_closeButton.Click += OnButtonClick;
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		base.OnKeyUp(e);
		if (e.Handled)
			return;

		switch (e.Key)
		{
			case Key.Escape:
				HideDialog(ContentDialogResult.None);
				e.Handled = true;
				break;
			case Key.Enter:
				if (DefaultButton != ContentDialogButton.None)
				{
					var result = DefaultButton switch
					{
						ContentDialogButton.Primary => ContentDialogResult.Primary,
						ContentDialogButton.Secondary => ContentDialogResult.Secondary,
						_ => ContentDialogResult.None
					};
					HideDialog(result);
					e.Handled = true;
				}
				break;
		}
	}

	/// <summary>
	/// 指定された TopLevel 上にダイアログを表示する
	/// </summary>
	public Task<ContentDialogResult> ShowAsync(TopLevel topLevel)
	{
		_tcs = new TaskCompletionSource<ContentDialogResult>();

		var ol = OverlayLayer.GetOverlayLayer(topLevel)
			?? throw new InvalidOperationException("OverlayLayer が見つかりません");

		_lastFocus = topLevel.FocusManager?.GetFocusedElement();

		_host ??= new DialogHost();
		_host.Content = this;
		ol.Children.Add(_host);

		IsVisible = true;
		PseudoClasses.Set(":open", true);

		// プライマリボタンまたは最初のボタンにフォーカス
		Loaded += (_, _) =>
		{
			var focusTarget = DefaultButton switch
			{
				ContentDialogButton.Primary => _primaryButton,
				ContentDialogButton.Secondary => _secondaryButton,
				ContentDialogButton.Close => _closeButton,
				_ => _primaryButton ?? _secondaryButton ?? _closeButton
			};
			focusTarget?.Focus();
		};

		return _tcs.Task;
	}

	/// <summary>
	/// 引数なしで呼び出す場合、アクティブなウィンドウを使用
	/// </summary>
	public Task<ContentDialogResult> ShowAsync()
	{
		TopLevel? topLevel = null;
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime al)
		{
			foreach (var w in al.Windows)
			{
				if (w.IsActive)
				{
					topLevel = w;
					break;
				}
			}
			topLevel ??= al.MainWindow;
		}
		else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime sl)
		{
			topLevel = TopLevel.GetTopLevel(sl.MainView);
		}

		return topLevel != null
			? ShowAsync(topLevel)
			: throw new InvalidOperationException("ContentDialog を表示するための TopLevel が見つかりません");
	}

	/// <summary>
	/// Window を指定して呼び出す（互換性のため）
	/// </summary>
	public Task<ContentDialogResult> ShowAsync(Window window) => ShowAsync((TopLevel)window);

	private void OnButtonClick(object? sender, RoutedEventArgs e)
	{
		if (sender == _primaryButton)
			HideDialog(ContentDialogResult.Primary);
		else if (sender == _secondaryButton)
			HideDialog(ContentDialogResult.Secondary);
		else if (sender == _closeButton)
			HideDialog(ContentDialogResult.None);
	}

	private void HideDialog(ContentDialogResult result)
	{
		PseudoClasses.Set(":open", false);
		IsVisible = false;

		if (_host?.Parent is Panel panel)
			panel.Children.Remove(_host);
		_host!.Content = null;

		// フォーカスを元に戻す
		(_lastFocus as InputElement)?.Focus();
		_lastFocus = null;

		_tcs?.TrySetResult(result);
	}

	/// <summary>
	/// OverlayLayer に配置するためのホストコントロール
	/// </summary>
	private class DialogHost : ContentControl
	{
		protected override Size MeasureOverride(Size availableSize)
		{
			return availableSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var child = Child;
			child?.Arrange(new Rect(finalSize));
			return finalSize;
		}
	}
}
