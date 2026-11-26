using SkiaSharp;
using System;
using System.IO;

namespace KyoshinEewViewer.Core;

/// <summary>
/// アプリで使用する Skia のフォント
/// </summary>
public static class KyoshinEewViewerFonts
{
	public static readonly SKTypeface MainRegular = Load("NotoSansJP.NotoSansJP-Regular.ttf");
	public static readonly SKTypeface MainBold = Load("NotoSansJP.NotoSansJP-Bold.ttf");
	public static readonly SKTypeface Inter = Load("Inter-Bold.ttf");
	public static readonly SKTypeface Icon = Load("FontAwesome6Free-Solid-900.otf");

	private static unsafe SKTypeface Load(string name)
	{
		var resourceName = "KyoshinEewViewer.Core.Assets.Fonts." + name;
		var stream = typeof(KyoshinEewViewerFonts).Assembly.GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException($"フォント {resourceName} が見つかりません");

		// 埋め込みリソースは読み込み済みのアセンブリ上のデータを直接指しているため、コピーせずに参照する。
		// アセンブリはプロセスの終了まで解放されないので、参照先も有効なままになる
		if (stream is UnmanagedMemoryStream { Position: 0 } memoryStream)
			return SKTypeface.FromData(SKData.Create((IntPtr)memoryStream.PositionPointer, (int)memoryStream.Length));

		using (stream)
			return SKTypeface.FromStream(stream);
	}
}
