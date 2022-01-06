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
	private static readonly SKTypeface IconSolidTypeface = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>()?.Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/FontAwesome5Free-Solid.ttf", UriKind.Absolute)));

	private Typeface[] CustomTypefaces { get; } = new[] {
			new Typeface("Noto Sans JP", weight: FontWeight.Regular),
			new Typeface("Noto Sans JP", weight: FontWeight.Bold),
			new Typeface("Font Awesome 5 Free", weight: FontWeight.Black),
		};
	private string DefaultFamilyName { get; } = "Noto Sans JP";

	public string GetDefaultFontFamilyName() => DefaultFamilyName;

	public IEnumerable<string> GetInstalledFontFamilyNames(bool checkForUpdates = false)
		=> CustomTypefaces.Select(x => x.FontFamily.Name);

	private readonly string[] _bcp47 = { CultureInfo.CurrentCulture.ThreeLetterISOLanguageName, CultureInfo.CurrentCulture.TwoLetterISOLanguageName };

	public bool TryMatchCharacter(int codepoint, FontStyle fontStyle, FontWeight fontWeight, FontFamily fontFamily, CultureInfo culture, out Typeface typeface)
	{
		foreach (var customTypeface in CustomTypefaces)
		{
			if (customTypeface.GlyphTypeface.GetGlyph((uint)codepoint) == 0)
				continue;

			typeface = new Typeface(customTypeface.FontFamily.Name, fontStyle, fontWeight);
			return true;
		}

		var fallback = SKFontManager.Default.MatchCharacter(fontFamily?.Name, (SKFontStyleWeight)fontWeight,
			SKFontStyleWidth.Normal, (SKFontStyleSlant)fontStyle, _bcp47, codepoint);

		typeface = new Typeface(fallback?.FamilyName ?? DefaultFamilyName, fontStyle, fontWeight);

		return true;
	}

	public IGlyphTypefaceImpl CreateGlyphTypeface(Typeface typeface)
	{
		var skTypeface = typeface.FontFamily.Name switch
		{
			FontFamily.DefaultFontFamilyName or "MainFont" => typeface.Weight == FontWeight.Bold ? MainBoldTypeface : MainRegularTypeface,
			"IconFont" => IconSolidTypeface,
			_ => MainRegularTypeface,
		};
		return new GlyphTypefaceImpl(skTypeface);
	}
}
