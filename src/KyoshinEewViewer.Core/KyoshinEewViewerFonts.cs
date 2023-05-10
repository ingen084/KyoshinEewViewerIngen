using Avalonia.Platform;
using Avalonia;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Core;

/// <summary>
/// アプリで使用する Skia のフォント
/// </summary>
public static class KyoshinEewViewerFonts
{
	public static readonly SKTypeface MainRegular = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>()?.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Regular.otf", UriKind.Absolute)));
	public static readonly SKTypeface MainBold = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>()?.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Bold.otf", UriKind.Absolute)));
}
