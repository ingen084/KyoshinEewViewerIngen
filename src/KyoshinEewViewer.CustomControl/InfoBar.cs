using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace KyoshinEewViewer.CustomControl;

/// <summary>
/// InfoBar の重大度レベル
/// </summary>
public enum InfoBarSeverity
{
	Informational,
	Success,
	Warning,
	Error
}

/// <summary>
/// 情報・警告・エラーを表示する通知バー
/// </summary>
[PseudoClasses(":informational", ":success", ":warning", ":error", ":closable")]
public class InfoBar : TemplatedControl
{
	public static readonly StyledProperty<bool> IsOpenProperty =
		AvaloniaProperty.Register<InfoBar, bool>(nameof(IsOpen), true);

	public static readonly StyledProperty<bool> IsClosableProperty =
		AvaloniaProperty.Register<InfoBar, bool>(nameof(IsClosable), true);

	public static readonly StyledProperty<InfoBarSeverity> SeverityProperty =
		AvaloniaProperty.Register<InfoBar, InfoBarSeverity>(nameof(Severity));

	public static readonly StyledProperty<string?> TitleProperty =
		AvaloniaProperty.Register<InfoBar, string?>(nameof(Title));

	public static readonly StyledProperty<string?> MessageProperty =
		AvaloniaProperty.Register<InfoBar, string?>(nameof(Message));

	/// <summary>
	/// 表示状態
	/// </summary>
	public bool IsOpen
	{
		get => GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}

	/// <summary>
	/// 閉じるボタンの表示
	/// </summary>
	public bool IsClosable
	{
		get => GetValue(IsClosableProperty);
		set => SetValue(IsClosableProperty, value);
	}

	/// <summary>
	/// 重大度
	/// </summary>
	public InfoBarSeverity Severity
	{
		get => GetValue(SeverityProperty);
		set => SetValue(SeverityProperty, value);
	}

	/// <summary>
	/// タイトル
	/// </summary>
	public string? Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <summary>
	/// メッセージ
	/// </summary>
	public string? Message
	{
		get => GetValue(MessageProperty);
		set => SetValue(MessageProperty, value);
	}

	private Button? _closeButton;

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		if (_closeButton != null)
			_closeButton.Click -= OnCloseButtonClick;

		base.OnApplyTemplate(e);

		_closeButton = e.NameScope.Find<Button>("CloseButton");
		if (_closeButton != null)
			_closeButton.Click += OnCloseButtonClick;

		UpdateSeverityPseudoClasses();
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == SeverityProperty)
			UpdateSeverityPseudoClasses();
		else if (change.Property == IsClosableProperty)
			PseudoClasses.Set(":closable", change.GetNewValue<bool>());
		else if (change.Property == IsOpenProperty)
			IsVisible = change.GetNewValue<bool>();
	}

	private void UpdateSeverityPseudoClasses()
	{
		PseudoClasses.Set(":informational", Severity == InfoBarSeverity.Informational);
		PseudoClasses.Set(":success", Severity == InfoBarSeverity.Success);
		PseudoClasses.Set(":warning", Severity == InfoBarSeverity.Warning);
		PseudoClasses.Set(":error", Severity == InfoBarSeverity.Error);
	}

	private void OnCloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		IsOpen = false;
	}
}
