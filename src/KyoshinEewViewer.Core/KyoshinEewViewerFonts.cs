using Avalonia.Platform;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Core;

/// <summary>
/// アプリで使用する Skia のフォント
/// </summary>
public static class KyoshinEewViewerFonts
{
	public static readonly SKTypeface MainRegular = SKTypeface.FromStream(AssetLoader.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Regular.otf", UriKind.Absolute)));
	public static readonly SKTypeface MainBold = SKTypeface.FromStream(AssetLoader.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Bold.otf", UriKind.Absolute)));
}
