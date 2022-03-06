using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace KyoshinEewViewer.Controls;

public class HyperlinkTextBlock : TextBlock
{
	public static readonly StyledProperty<Uri?> UriProperty =
	AvaloniaProperty.Register<HyperlinkTextBlock, Uri?>(nameof(Uri));

	public Uri? Uri
	{
		get => GetValue(UriProperty);
		set => SetValue(UriProperty, value);
	}

	public HyperlinkTextBlock() : base()
	{
		Cursor = new Cursor(StandardCursorType.Hand);
		TextDecorations = new TextDecorationCollection()
		{
			new TextDecoration() { Location = TextDecorationLocation.Underline },
		};
		Tapped += (s, e) =>
		{
			if (Uri == null)
				return;
			UrlOpener.OpenUrl(Uri.ToString());
		};
	}
}
