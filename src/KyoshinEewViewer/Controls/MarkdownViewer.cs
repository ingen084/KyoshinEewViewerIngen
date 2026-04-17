using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using LiveMarkdown.Avalonia;
using Splat;
using System;
using System.IO;

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
		_renderer.LinkClick += (_, e) => UrlOpener.OpenUrl(e.HRef.ToString());
		Content = _renderer;
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
			LogHost.Default.Warn(ex, $"Markdown リソース {source} の読み込みに失敗しました");
			return null;
		}
	}
}
