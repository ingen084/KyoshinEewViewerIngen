using Avalonia.Platform;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Core;

/// <summary>
/// アプリで使用する Skia のフォント
/// </summary>
public static class KyoshinEewViewerFonts
{
	public static readonly SKTypeface MainRegular = SKTypeface.FromStream(AssetLoader.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP/NotoSansJP-Regular.ttf", UriKind.Absolute)));
	public static readonly SKTypeface MainBold = SKTypeface.FromStream(AssetLoader.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP/NotoSansJP-Bold.ttf", UriKind.Absolute)));
}
