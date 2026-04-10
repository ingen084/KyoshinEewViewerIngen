using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace KyoshinEewViewer.CustomControl;

/// <summary>
/// Windows 11 の設定アプリ風の設定項目コントロール
/// </summary>
[TemplatePart("PART_Expander", typeof(Expander))]
[TemplatePart("PART_ContentHost", typeof(SettingsExpanderItem))]
[PseudoClasses(":empty")]
public class SettingsExpander : HeaderedItemsControl
{
	public static readonly StyledProperty<string?> DescriptionProperty =
		AvaloniaProperty.Register<SettingsExpander, string?>(nameof(Description));

	public static readonly StyledProperty<object?> FooterProperty =
		AvaloniaProperty.Register<SettingsExpander, object?>(nameof(Footer));

	public static readonly StyledProperty<string?> IconGlyphProperty =
		AvaloniaProperty.Register<SettingsExpander, string?>(nameof(IconGlyph));

	public static readonly StyledProperty<bool> IsExpandedProperty =
		Expander.IsExpandedProperty.AddOwner<SettingsExpander>();

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

	/// <summary>
	/// 展開状態
	/// </summary>
	public bool IsExpanded
	{
		get => GetValue(IsExpandedProperty);
		set => SetValue(IsExpandedProperty, value);
	}

	private Expander? _expander;
	private ToggleButton? _expanderToggleButton;
	private SettingsExpanderItem? _contentHost;

	public SettingsExpander()
	{
		ItemsView.CollectionChanged += (_, _) =>
		{
			var hasItems = ItemCount > 0;
			PseudoClasses.Set(":empty", !hasItems);
			UpdateToggleButtonState(hasItems);
			UpdateIconPlaceholders();
		};
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		_expander = e.NameScope.Find<Expander>("PART_Expander");
		_contentHost = e.NameScope.Find<SettingsExpanderItem>("PART_ContentHost");

		if (_expander != null)
		{
			_expander.Loaded += (_, _) =>
			{
				_expanderToggleButton = _expander.GetTemplateChildren()
					.OfType<ToggleButton>()
					.FirstOrDefault();
				UpdateToggleButtonState(ItemCount > 0);
			};
		}

		PseudoClasses.Set(":empty", ItemCount == 0);
		UpdateIconPlaceholders();
	}

	private void UpdateToggleButtonState(bool hasItems)
	{
		if (_expanderToggleButton != null)
			((IPseudoClasses)_expanderToggleButton.Classes).Set(":hasItems", hasItems);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsExpandedProperty)
		{
			// 子アイテムがない場合は展開を防止
			if (ItemCount == 0 && change.GetNewValue<bool>() && this.IsAttachedToVisualTree())
			{
				Dispatcher.UIThread.Post(() => IsExpanded = false, DispatcherPriority.Send);
			}
		}
	}

	protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
	{
		var isItem = item is SettingsExpanderItem;
		recycleKey = isItem ? null : nameof(SettingsExpanderItem);
		return !isItem;
	}

	protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
	{
		return new SettingsExpanderItem();
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		var sz = base.MeasureOverride(availableSize);
		UpdateIconPlaceholders();
		return sz;
	}

	internal void UpdateIconPlaceholders()
	{
		if (_contentHost == null)
			return;

		if (ItemCount == 0)
			return;

		// 子アイテムにアイコンがあるかチェック
		var hasIcon = IconGlyph != null;
		if (!hasIcon)
		{
			foreach (var container in GetRealizedContainers())
			{
				if (container is SettingsExpanderItem sei && sei.IconGlyph != null)
				{
					hasIcon = true;
					break;
				}
			}
		}

		_contentHost.SetIconPlaceholder(hasIcon);
		foreach (var container in GetRealizedContainers())
		{
			if (container is SettingsExpanderItem sei)
				sei.SetIconPlaceholder(hasIcon);
		}
	}
}
