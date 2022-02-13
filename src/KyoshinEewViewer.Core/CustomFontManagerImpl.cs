using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KyoshinEewViewer.Core;

// 参考元: https://github.com/AvaloniaUI/Avalonia/issues/4427#issuecomment-769767881
public class CustomFontManagerImpl : IFontManagerImpl
{
	private static readonly SKTypeface MainRegularTypeface = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>()?.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Regular.otf", UriKind.Absolute)));
	private static readonly SKTypeface MainBoldTypeface = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>()?.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Bold.otf", UriKind.Absolute)));
	private static readonly SKTypeface IconTypeface = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>()?.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/FontAwesome6Free-Solid-900.otf", UriKind.Absolute)));

	private Typeface[] CustomTypefaces { get; } = new[] {
			new Typeface("Font Awesome 6 Free", weight: FontWeight.Black),
			new Typeface("Noto Sans JP", weight: FontWeight.Regular),
			new Typeface("Noto Sans JP", weight: FontWeight.Bold),
		};
	private string DefaultFamilyName { get; } = "MainFont";

	public string GetDefaultFontFamilyName() => DefaultFamilyName;

	public IEnumerable<string> GetInstalledFontFamilyNames(bool checkForUpdates = false)
		=> new[] { "MainFont", "IconFont" };

	private readonly string[] _bcp47 = { CultureInfo.CurrentCulture.ThreeLetterISOLanguageName, CultureInfo.CurrentCulture.TwoLetterISOLanguageName };

	public bool TryMatchCharacter(int codepoint, FontStyle fontStyle, FontWeight fontWeight, FontFamily? fontFamily, CultureInfo? culture, out Typeface typeface)
	{
		foreach (var customTypeface in CustomTypefaces)
		{
			if (customTypeface.GlyphTypeface.GetGlyph((uint)codepoint) == 0)
				continue;

			typeface = new Typeface(customTypeface.FontFamily?.Name ?? "MainFont", fontStyle, fontWeight);
			return true;
		}

		var fallback = SKFontManager.Default.MatchCharacter(fontFamily?.Name, (SKFontStyleWeight)fontWeight,
			SKFontStyleWidth.Normal, (SKFontStyleSlant)fontStyle, _bcp47, codepoint);

		typeface = new Typeface(fallback?.FamilyName ?? DefaultFamilyName, fontStyle, fontWeight);

		return true;
	}

	public IGlyphTypefaceImpl CreateGlyphTypeface(Typeface typeface)
	{
		var skTypeface = typeface.FontFamily?.Name switch
		{
			FontFamily.DefaultFontFamilyName or "MainFont" => typeface.Weight == FontWeight.Bold ? MainBoldTypeface : MainRegularTypeface,
			"IconFont" => IconTypeface,
			_ => MainRegularTypeface,
		};
		return new GlyphTypefaceImpl(skTypeface);
	}
}
