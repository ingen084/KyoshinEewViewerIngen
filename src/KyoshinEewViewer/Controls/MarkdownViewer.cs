using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;
using LiveMarkdown.Avalonia;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Controls;

/// <summary>
/// LiveMarkdown.Avalonia の MarkdownRenderer をラップし、
/// avares:// リソースや文字列リテラルからの Markdown 表示を扱いやすくするラッパーコントロール
/// </summary>
public class MarkdownViewer : ContentControl
{
	public static readonly StyledProperty<string?> SourceProperty =
		AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Source));

	public static readonly StyledProperty<string?> MarkdownProperty =
		AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

	/// <summary>
	/// 表示する Markdown のリソース URI (例: avares://... )
	/// 設定されている場合は <see cref="Markdown"/> より優先される
	/// </summary>
	public string? Source
	{
		get => GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	/// <summary>
	/// 表示する Markdown テキスト
	/// </summary>
	public string? Markdown
	{
		get => GetValue(MarkdownProperty);
		set => SetValue(MarkdownProperty, value);
	}

	private readonly ObservableStringBuilder _builder = new();
	private readonly MarkdownRenderer _renderer;

	public MarkdownViewer()
	{
		_renderer = new MarkdownRenderer { MarkdownBuilder = _builder };
		_renderer.LinkClick += (_, e) =>
		{
			if (e.HRef is { } href)
				UrlOpener.OpenUrl(href.ToString());
		};
		AttachLinkClickWorkaround(_renderer);
		Content = _renderer;
	}

	private static readonly FieldInfo? PressingLinkField =
		typeof(MarkdownTextBlock).GetField("pressingLink", BindingFlags.NonPublic | BindingFlags.Instance);
	private static readonly FieldInfo? PointerLinkField =
		typeof(MarkdownTextBlock).GetField("pointerLink", BindingFlags.NonPublic | BindingFlags.Instance);

	/// <summary>
	/// LiveMarkdown.Avalonia 2.2.0 のリンククリック不能バグの回避策<br/>
	/// MarkdownRenderer がテキスト選択のために PointerPressed でポインターを自身にキャプチャするため、
	/// LinkClick の発火元である MarkdownTextBlock.OnPointerReleased にイベントが届かずリンクが機能しない。
	/// リンク上での押下時のみキャプチャ先をテキストブロックに付け替えて解放イベントを届くようにする。
	/// ライブラリ側で修正されたら削除すること。
	/// </summary>
	private static void AttachLinkClickWorkaround(MarkdownRenderer renderer)
	{
		renderer.AddHandler(PointerPressedEvent, (_, e) =>
		{
			if (!ReferenceEquals(e.Pointer.Captured, renderer))
				return;

			var block = e.Source as MarkdownTextBlock
				?? renderer.GetVisualsAt(e.GetPosition(renderer)).OfType<MarkdownTextBlock>().FirstOrDefault();

			if (block is not null && block.Classes.Contains(":pointerover-link"))
				e.Pointer.Capture(block);
		}, RoutingStrategies.Bubble, handledEventsToo: true);

		// レンダラーのキャプチャで MarkdownTextBlock に PointerExited が飛び内部の pointerLink が
		// null 化されるため、解放時のリンク判定 (pressingLink == pointerLink) が成立するよう
		// トンネル段階 (OnPointerReleased より前) で押下時のリンクを復元する
		renderer.AddHandler(PointerReleasedEvent, (_, e) =>
		{
			var block = e.Pointer.Captured as MarkdownTextBlock ?? e.Source as MarkdownTextBlock;
			// ドラッグ中の PointerMoved は疑似クラスを更新するため、リンク外で離した場合は復元しない
			if (block is null || !block.Classes.Contains(":pointerover-link"))
				return;

			var pressingLink = PressingLinkField?.GetValue(block);
			if (pressingLink is not null)
				PointerLinkField?.SetValue(block, pressingLink);
		}, RoutingStrategies.Tunnel, handledEventsToo: true);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == SourceProperty || change.Property == MarkdownProperty)
			UpdateContent();
	}

	private void UpdateContent()
	{
		_builder.Clear();
		var text = !string.IsNullOrEmpty(Source) ? LoadFromSource(Source!) : Markdown;
		if (!string.IsNullOrEmpty(text))
			_builder.Append(text);
	}

	private static string? LoadFromSource(string source)
	{
		try
		{
			using var stream = AssetLoader.Open(new Uri(source, UriKind.Absolute));
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}
		catch (Exception ex)
		{
			AppLog.Default.LogWarning(ex, "Markdown リソース {Source} の読み込みに失敗しました", source);
			return null;
		}
	}
}
