using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace KyoshinEewViewer.CustomControl;

/// <summary>
/// SettingsExpander 内に表示される設定項目
/// </summary>
[PseudoClasses(":footer", ":content", ":description", ":icon", ":iconPlaceholder")]
public class SettingsExpanderItem : ContentControl
{
	public static readonly StyledProperty<string?> DescriptionProperty =
		AvaloniaProperty.Register<SettingsExpanderItem, string?>(nameof(Description));

	public static readonly StyledProperty<object?> FooterProperty =
		AvaloniaProperty.Register<SettingsExpanderItem, object?>(nameof(Footer));

	public static readonly StyledProperty<string?> IconGlyphProperty =
		AvaloniaProperty.Register<SettingsExpanderItem, string?>(nameof(IconGlyph));

	/// <summary>
	/// 説明テキスト
	/// </summary>
	public string? Description
	{
		get => GetValue(DescriptionProperty);
		set => SetValue(DescriptionProperty, value);
	}

	/// <summary>
	/// 右側に表示されるフッターコンテンツ
	/// </summary>
	public object? Footer
	{
		get => GetValue(FooterProperty);
		set => SetValue(FooterProperty, value);
	}

	/// <summary>
	/// アイコンのグリフ文字
	/// </summary>
	public string? IconGlyph
	{
		get => GetValue(IconGlyphProperty);
		set => SetValue(IconGlyphProperty, value);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == FooterProperty)
			PseudoClasses.Set(":footer", change.NewValue != null);
		else if (change.Property == ContentProperty)
			PseudoClasses.Set(":content", change.NewValue != null);
		else if (change.Property == DescriptionProperty)
			PseudoClasses.Set(":description", change.NewValue != null);
		else if (change.Property == IconGlyphProperty)
		{
			PseudoClasses.Set(":icon", change.NewValue != null);
			// 親の SettingsExpander に通知
			if (this.Parent is SettingsExpander se)
				se.UpdateIconPlaceholders();
		}
	}

	protected override bool RegisterContentPresenter(ContentPresenter presenter)
	{
		if (presenter.Name is "ContentPresenter" or "FooterPresenter")
			return true;

		return base.RegisterContentPresenter(presenter);
	}

	internal void SetIconPlaceholder(bool usePlaceholder)
	{
		PseudoClasses.Set(":iconPlaceholder", usePlaceholder);
	}
}
