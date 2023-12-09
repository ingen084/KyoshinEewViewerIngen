using Avalonia.Controls;
using Avalonia.Media;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Core.Models;

public class IntensityTheme
{
	public required string Name { get; init; }
	public required Dictionary<JmaIntensity, IntensityColor> IntensityColors { get; init; }

	public required Dictionary<LpgmIntensity, IntensityColor> LpgmIntensityColors { get; init; }

	public required float BorderWidthMultiply { get; init; }

	public record IntensityColor(string Foreground, string Background, string Border);

	public ResourceDictionary CreateResourceDictionary()
	{
		Color GetColor(Func<IntensityTheme, string> propertySelector)
		{
			if (Color.TryParse(propertySelector(this), out var color))
				return color;
			// フォールバックさせる
			// ここでのエラーは検知させるためなにもしない
			return Color.Parse(propertySelector(Standard));
		}

		var dict = new ResourceDictionary();
		foreach (var i in Enum.GetValues<JmaIntensity>())
		{
			dict.Add($"{i}Foreground", GetColor(x => x.IntensityColors[i].Foreground));
			dict.Add($"{i}Background", GetColor(x => x.IntensityColors[i].Background));
			dict.Add($"{i}Border", GetColor(x => x.IntensityColors[i].Border));
		}
		foreach (var i in Enum.GetValues<LpgmIntensity>())
		{
			if (i is LpgmIntensity.Unknown or LpgmIntensity.Error)
				continue;
			dict.Add($"{i}Foreground", GetColor(x => x.LpgmIntensityColors[i].Foreground));
			dict.Add($"{i}Background", GetColor(x => x.LpgmIntensityColors[i].Background));
			dict.Add($"{i}Border", GetColor(x => x.LpgmIntensityColors[i].Border));
		}
		dict.Add("BorderWidthMultiply", BorderWidthMultiply);
		return dict;
	}

	public static IntensityTheme Standard { get; } = new()
	{
		Name = "Standard",
		IntensityColors = new()
		{
			{ JmaIntensity.Unknown , new("#000000", "#808080", "#CCCCCC") },
			{ JmaIntensity.Error , new("#b30f20", "#ffff6c", "#FFFF52") },
			{ JmaIntensity.Int0 , new("#E6E6E6", "#808080", "#CCCCCC") },
			{ JmaIntensity.Int1 , new("#000000", "#51b3fc", "#9DD3FC") },
			{ JmaIntensity.Int2 , new("#000000", "#7dd45a", "#ACED93") },
			{ JmaIntensity.Int3 , new("#000000", "#f0de7e", "#F0EAC5") },
			{ JmaIntensity.Int4 , new("#000000", "#fa782c", "#FAA878") },
			{ JmaIntensity.Int5Lower , new("#FFFFFF", "#b30f20", "#CC4E5B") },
			{ JmaIntensity.Int5Upper , new("#FFFFFF", "#b30f20", "#CC4E5B") },
			{ JmaIntensity.Int6Lower , new("#b30f20", "#ffcdde", "#FFFFFF") },
			{ JmaIntensity.Int6Upper , new("#b30f20", "#ffcdde", "#FFFFFF") },
			{ JmaIntensity.Int7 , new("#FFFFFF", "#9400d3", "#ffff6c") },
		},
		LpgmIntensityColors = new()
		{
			{ LpgmIntensity.Unknown , new("#000000", "#808080", "#CCCCCC") },
			{ LpgmIntensity.Error , new("#b30f20", "#ffff6c", "#FFFF52") },
			{ LpgmIntensity.LpgmInt0 , new("#E6E6E6", "#808080", "#CCCCCC") },
			{ LpgmIntensity.LpgmInt1 , new("#000000", "#17BBD6", "#58C5D6") },
			{ LpgmIntensity.LpgmInt2 , new("#000000", "#F0FF09", "#FAFFB2") },
			{ LpgmIntensity.LpgmInt3 , new("#FFFFFF", "#D64700", "#F08048") },
			{ LpgmIntensity.LpgmInt4 , new("#FFFFFF", "#990033", "#B2365F") },
		},
		BorderWidthMultiply = 0.125f,
	};

	public static IntensityTheme Jma { get; } = new()
	{
		Name = "JMA",
		IntensityColors = new()
		{
			{ JmaIntensity.Unknown , new("#000000", "#808080", "#000000") },
			{ JmaIntensity.Error , new("#b30f20", "#ffff6c", "#000000") },
			{ JmaIntensity.Int0 , new("#FFFFFF", "#808080", "#000000") },
			{ JmaIntensity.Int1 , new("#000000", "#F2F2FF", "#000000") },
			{ JmaIntensity.Int2 , new("#000000", "#00AAFF", "#000000") },
			{ JmaIntensity.Int3 , new("#FFFFFF", "#0041FF", "#000000") },
			{ JmaIntensity.Int4 , new("#000000", "#FAE696", "#000000") },
			{ JmaIntensity.Int5Lower , new("#000000", "#FFE600", "#000000") },
			{ JmaIntensity.Int5Upper , new("#000000", "#FF9900", "#000000") },
			{ JmaIntensity.Int6Lower , new("#FFFFFF", "#FF2800", "#000000") },
			{ JmaIntensity.Int6Upper , new("#FFFFFF", "#A50021", "#000000") },
			{ JmaIntensity.Int7 , new("#FFFFFF", "#B40068", "#000000") },
		},
		LpgmIntensityColors = new()
		{
			{ LpgmIntensity.Unknown , new("#000000", "#808080", "#000000") },
			{ LpgmIntensity.Error , new("#b30f20", "#ffff6c", "#000000") },
			{ LpgmIntensity.LpgmInt0 , new("#FFFFFF", "#808080", "#000000") },
			{ LpgmIntensity.LpgmInt1 , new("#FFFFFF", "#0041FF", "#000000") },
			{ LpgmIntensity.LpgmInt2 , new("#000000", "#FFE600", "#000000") },
			{ LpgmIntensity.LpgmInt3 , new("#FFFFFF", "#FF2800", "#000000") },
			{ LpgmIntensity.LpgmInt4 , new("#FFFFFF", "#A50021", "#000000") },
		},
		BorderWidthMultiply = 0.05f,
	};

	public static IntensityTheme Quarog { get; } = new()
	{
		Name = "Quarog",
		IntensityColors = new()
		{
			{ JmaIntensity.Unknown , new("#F5FFFFFF", "#1e2832", "#171F27") },
			{ JmaIntensity.Error , new("#F5FFFFFF", "#5a646e", "#4F5860") },
			{ JmaIntensity.Int0 , new("#F5a0aab4", "#5a646e", "#4F5860") },
			{ JmaIntensity.Int1 , new("#F5FFFFFF", "#325a8c", "#294A73") },
			{ JmaIntensity.Int2 , new("#F5FFFFFF", "#3278d2", "#2C69B8") },
			{ JmaIntensity.Int3 , new("#F51E2832", "#32d2e6", "#2CBBCC") },
			{ JmaIntensity.Int4 , new("#F51e2832", "#fafa8c", "#E0E07E") },
			{ JmaIntensity.Int5Lower , new("#F51e2832", "#fabe32", "#E0AB2D") },
			{ JmaIntensity.Int5Upper , new("#F51e2832", "#fa821e", "#E0751B") },
			{ JmaIntensity.Int6Lower , new("#F5FFFFFF", "#e61414", "#CC1212") },
			{ JmaIntensity.Int6Upper , new("#F5FFFFFF", "#a01432", "#86112A") },
			{ JmaIntensity.Int7 , new("#F5FFFFFF", "#5a1446", "#400E32") },
		},
		LpgmIntensityColors = new()
		{
			{ LpgmIntensity.Unknown , new("#F5FFFFFF", "#1e2832", "#171F27") },
			{ LpgmIntensity.Error , new("#F5FFFFFF", "#5a646e", "#4F5860") },
			{ LpgmIntensity.LpgmInt0 , new("#F5a0aab4", "#5a646e", "#4F5860") },
			{ LpgmIntensity.LpgmInt1 , new("#F51E2832", "#32d2e6", "#2CBBCC") },
			{ LpgmIntensity.LpgmInt2 , new("#F51e2832", "#fabe32", "#E0AB2D") },
			{ LpgmIntensity.LpgmInt3 , new("#F5FFFFFF", "#e61414", "#CC1212") },
			{ LpgmIntensity.LpgmInt4 , new("#F5FFFFFF", "#a01432", "#86112A") },
		},
		BorderWidthMultiply = 0.125f,
	};

	public static IntensityTheme Vivid { get; } = new()
	{
		Name = "Vivid",
		IntensityColors = new()
		{
			{ JmaIntensity.Unknown , new("#000000", "#808080", "#000000") },
			{ JmaIntensity.Error , new("#b30f20", "#ffff6c", "#000000") },
			{ JmaIntensity.Int0 , new("#FFFFFF", "#808080", "#000000") },
			{ JmaIntensity.Int1 , new("#a9a9a9", "#fffafa", "#000000") },
			{ JmaIntensity.Int2 , new("#FFFFFF", "#1e90ff", "#000000") },
			{ JmaIntensity.Int3 , new("#FFFFFF", "#3cb371", "#000000") },
			{ JmaIntensity.Int4 , new("#FFFFFF", "#daa520", "#000000") },
			{ JmaIntensity.Int5Lower , new("#FFFFFF", "#ff8c00", "#000000") },
			{ JmaIntensity.Int5Upper , new("#FFFFFF", "#ff8c00", "#000000") },
			{ JmaIntensity.Int6Lower , new("#FFFFFF", "#dc143c", "#000000") },
			{ JmaIntensity.Int6Upper , new("#FFFFFF", "#dc143c", "#000000") },
			{ JmaIntensity.Int7 , new("#FFFFFF", "#ff1493", "#000000") },
		},
		LpgmIntensityColors = new()
		{
			{ LpgmIntensity.Unknown , new("#000000", "#808080", "#000000") },
			{ LpgmIntensity.Error , new("#b30f20", "#ffff6c", "#000000") },
			{ LpgmIntensity.LpgmInt0 , new("#E6E6E6", "#808080", "#CCCCCC") },
			{ LpgmIntensity.LpgmInt1 , new("#000000", "#17BBD6", "#58C5D6") },
			{ LpgmIntensity.LpgmInt2 , new("#000000", "#F0FF09", "#FAFFB2") },
			{ LpgmIntensity.LpgmInt3 , new("#FFFFFF", "#D64700", "#F08048") },
			{ LpgmIntensity.LpgmInt4 , new("#FFFFFF", "#990033", "#B2365F") },
		},
		BorderWidthMultiply = 0.05f,
	};

	public static IntensityTheme[] DefaultThemes { get; } = [Standard, Vivid, Quarog, Jma];
}
