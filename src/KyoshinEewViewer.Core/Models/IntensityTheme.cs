using Avalonia.Controls;
using Avalonia.Media;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core.Models;

public class IntensityTheme : ReactiveObject
{
	private string _name = string.Empty;
	public required string Name
	{
		get => _name;
		set => this.RaiseAndSetIfChanged(ref _name, value);
	}

	private float _borderWidthMultiply = 0.125f;
	public float BorderWidthMultiply
	{
		get => _borderWidthMultiply;
		set => this.RaiseAndSetIfChanged(ref _borderWidthMultiply, value);
	}

	// JmaIntensity色設定のプロパティ
	private string _unknownForeground = "";
	public string UnknownForeground
	{
		get => _unknownForeground;
		set => this.RaiseAndSetIfChanged(ref _unknownForeground, value);
	}

	private string _unknownBackground = "";
	public string UnknownBackground
	{
		get => _unknownBackground;
		set => this.RaiseAndSetIfChanged(ref _unknownBackground, value);
	}

	private string _unknownBorder = "";
	public string UnknownBorder
	{
		get => _unknownBorder;
		set => this.RaiseAndSetIfChanged(ref _unknownBorder, value);
	}

	private string _errorForeground = "";
	public string ErrorForeground
	{
		get => _errorForeground;
		set => this.RaiseAndSetIfChanged(ref _errorForeground, value);
	}

	private string _errorBackground = "";
	public string ErrorBackground
	{
		get => _errorBackground;
		set => this.RaiseAndSetIfChanged(ref _errorBackground, value);
	}

	private string _errorBorder = "";
	public string ErrorBorder
	{
		get => _errorBorder;
		set => this.RaiseAndSetIfChanged(ref _errorBorder, value);
	}

	private string _int0Foreground = "";
	public string Int0Foreground
	{
		get => _int0Foreground;
		set => this.RaiseAndSetIfChanged(ref _int0Foreground, value);
	}

	private string _int0Background = "";
	public string Int0Background
	{
		get => _int0Background;
		set => this.RaiseAndSetIfChanged(ref _int0Background, value);
	}

	private string _int0Border = "";
	public string Int0Border
	{
		get => _int0Border;
		set => this.RaiseAndSetIfChanged(ref _int0Border, value);
	}

	private string _int1Foreground = "";
	public string Int1Foreground
	{
		get => _int1Foreground;
		set => this.RaiseAndSetIfChanged(ref _int1Foreground, value);
	}

	private string _int1Background = "";
	public string Int1Background
	{
		get => _int1Background;
		set => this.RaiseAndSetIfChanged(ref _int1Background, value);
	}

	private string _int1Border = "";
	public string Int1Border
	{
		get => _int1Border;
		set => this.RaiseAndSetIfChanged(ref _int1Border, value);
	}

	private string _int2Foreground = "";
	public string Int2Foreground
	{
		get => _int2Foreground;
		set => this.RaiseAndSetIfChanged(ref _int2Foreground, value);
	}

	private string _int2Background = "";
	public string Int2Background
	{
		get => _int2Background;
		set => this.RaiseAndSetIfChanged(ref _int2Background, value);
	}

	private string _int2Border = "";
	public string Int2Border
	{
		get => _int2Border;
		set => this.RaiseAndSetIfChanged(ref _int2Border, value);
	}

	private string _int3Foreground = "";
	public string Int3Foreground
	{
		get => _int3Foreground;
		set => this.RaiseAndSetIfChanged(ref _int3Foreground, value);
	}

	private string _int3Background = "";
	public string Int3Background
	{
		get => _int3Background;
		set => this.RaiseAndSetIfChanged(ref _int3Background, value);
	}

	private string _int3Border = "";
	public string Int3Border
	{
		get => _int3Border;
		set => this.RaiseAndSetIfChanged(ref _int3Border, value);
	}

	private string _int4Foreground = "";
	public string Int4Foreground
	{
		get => _int4Foreground;
		set => this.RaiseAndSetIfChanged(ref _int4Foreground, value);
	}

	private string _int4Background = "";
	public string Int4Background
	{
		get => _int4Background;
		set => this.RaiseAndSetIfChanged(ref _int4Background, value);
	}

	private string _int4Border = "";
	public string Int4Border
	{
		get => _int4Border;
		set => this.RaiseAndSetIfChanged(ref _int4Border, value);
	}

	private string _int5LowerForeground = "";
	public string Int5LowerForeground
	{
		get => _int5LowerForeground;
		set => this.RaiseAndSetIfChanged(ref _int5LowerForeground, value);
	}

	private string _int5LowerBackground = "";
	public string Int5LowerBackground
	{
		get => _int5LowerBackground;
		set => this.RaiseAndSetIfChanged(ref _int5LowerBackground, value);
	}

	private string _int5LowerBorder = "";
	public string Int5LowerBorder
	{
		get => _int5LowerBorder;
		set => this.RaiseAndSetIfChanged(ref _int5LowerBorder, value);
	}

	private string _int5UpperForeground = "";
	public string Int5UpperForeground
	{
		get => _int5UpperForeground;
		set => this.RaiseAndSetIfChanged(ref _int5UpperForeground, value);
	}

	private string _int5UpperBackground = "";
	public string Int5UpperBackground
	{
		get => _int5UpperBackground;
		set => this.RaiseAndSetIfChanged(ref _int5UpperBackground, value);
	}

	private string _int5UpperBorder = "";
	public string Int5UpperBorder
	{
		get => _int5UpperBorder;
		set => this.RaiseAndSetIfChanged(ref _int5UpperBorder, value);
	}

	private string _int6LowerForeground = "";
	public string Int6LowerForeground
	{
		get => _int6LowerForeground;
		set => this.RaiseAndSetIfChanged(ref _int6LowerForeground, value);
	}

	private string _int6LowerBackground = "";
	public string Int6LowerBackground
	{
		get => _int6LowerBackground;
		set => this.RaiseAndSetIfChanged(ref _int6LowerBackground, value);
	}

	private string _int6LowerBorder = "";
	public string Int6LowerBorder
	{
		get => _int6LowerBorder;
		set => this.RaiseAndSetIfChanged(ref _int6LowerBorder, value);
	}

	private string _int6UpperForeground = "";
	public string Int6UpperForeground
	{
		get => _int6UpperForeground;
		set => this.RaiseAndSetIfChanged(ref _int6UpperForeground, value);
	}

	private string _int6UpperBackground = "";
	public string Int6UpperBackground
	{
		get => _int6UpperBackground;
		set => this.RaiseAndSetIfChanged(ref _int6UpperBackground, value);
	}

	private string _int6UpperBorder = "";
	public string Int6UpperBorder
	{
		get => _int6UpperBorder;
		set => this.RaiseAndSetIfChanged(ref _int6UpperBorder, value);
	}

	private string _int7Foreground = "";
	public string Int7Foreground
	{
		get => _int7Foreground;
		set => this.RaiseAndSetIfChanged(ref _int7Foreground, value);
	}

	private string _int7Background = "";
	public string Int7Background
	{
		get => _int7Background;
		set => this.RaiseAndSetIfChanged(ref _int7Background, value);
	}

	private string _int7Border = "";
	public string Int7Border
	{
		get => _int7Border;
		set => this.RaiseAndSetIfChanged(ref _int7Border, value);
	}

	// LpgmIntensity色設定のプロパティ
	private string _lpgmUnknownForeground = "";
	public string LpgmUnknownForeground
	{
		get => _lpgmUnknownForeground;
		set => this.RaiseAndSetIfChanged(ref _lpgmUnknownForeground, value);
	}

	private string _lpgmUnknownBackground = "";
	public string LpgmUnknownBackground
	{
		get => _lpgmUnknownBackground;
		set => this.RaiseAndSetIfChanged(ref _lpgmUnknownBackground, value);
	}

	private string _lpgmUnknownBorder = "";
	public string LpgmUnknownBorder
	{
		get => _lpgmUnknownBorder;
		set => this.RaiseAndSetIfChanged(ref _lpgmUnknownBorder, value);
	}

	private string _lpgmErrorForeground = "";
	public string LpgmErrorForeground
	{
		get => _lpgmErrorForeground;
		set => this.RaiseAndSetIfChanged(ref _lpgmErrorForeground, value);
	}

	private string _lpgmErrorBackground = "";
	public string LpgmErrorBackground
	{
		get => _lpgmErrorBackground;
		set => this.RaiseAndSetIfChanged(ref _lpgmErrorBackground, value);
	}

	private string _lpgmErrorBorder = "";
	public string LpgmErrorBorder
	{
		get => _lpgmErrorBorder;
		set => this.RaiseAndSetIfChanged(ref _lpgmErrorBorder, value);
	}

	private string _lpgmInt0Foreground = "";
	public string LpgmInt0Foreground
	{
		get => _lpgmInt0Foreground;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt0Foreground, value);
	}

	private string _lpgmInt0Background = "";
	public string LpgmInt0Background
	{
		get => _lpgmInt0Background;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt0Background, value);
	}

	private string _lpgmInt0Border = "";
	public string LpgmInt0Border
	{
		get => _lpgmInt0Border;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt0Border, value);
	}

	private string _lpgmInt1Foreground = "";
	public string LpgmInt1Foreground
	{
		get => _lpgmInt1Foreground;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt1Foreground, value);
	}

	private string _lpgmInt1Background = "";
	public string LpgmInt1Background
	{
		get => _lpgmInt1Background;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt1Background, value);
	}

	private string _lpgmInt1Border = "";
	public string LpgmInt1Border
	{
		get => _lpgmInt1Border;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt1Border, value);
	}

	private string _lpgmInt2Foreground = "";
	public string LpgmInt2Foreground
	{
		get => _lpgmInt2Foreground;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt2Foreground, value);
	}

	private string _lpgmInt2Background = "";
	public string LpgmInt2Background
	{
		get => _lpgmInt2Background;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt2Background, value);
	}

	private string _lpgmInt2Border = "";
	public string LpgmInt2Border
	{
		get => _lpgmInt2Border;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt2Border, value);
	}

	private string _lpgmInt3Foreground = "";
	public string LpgmInt3Foreground
	{
		get => _lpgmInt3Foreground;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt3Foreground, value);
	}

	private string _lpgmInt3Background = "";
	public string LpgmInt3Background
	{
		get => _lpgmInt3Background;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt3Background, value);
	}

	private string _lpgmInt3Border = "";
	public string LpgmInt3Border
	{
		get => _lpgmInt3Border;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt3Border, value);
	}

	private string _lpgmInt4Foreground = "";
	public string LpgmInt4Foreground
	{
		get => _lpgmInt4Foreground;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt4Foreground, value);
	}

	private string _lpgmInt4Background = "";
	public string LpgmInt4Background
	{
		get => _lpgmInt4Background;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt4Background, value);
	}

	private string _lpgmInt4Border = "";
	public string LpgmInt4Border
	{
		get => _lpgmInt4Border;
		set => this.RaiseAndSetIfChanged(ref _lpgmInt4Border, value);
	}

	// インデクサによる互換性アクセス (getterのみ)
	[JsonIgnore]
	public IntensityColor this[JmaIntensity intensity] => intensity switch
	{
		JmaIntensity.Unknown => new(UnknownForeground, UnknownBackground, UnknownBorder),
		JmaIntensity.Error => new(ErrorForeground, ErrorBackground, ErrorBorder),
		JmaIntensity.Int0 => new(Int0Foreground, Int0Background, Int0Border),
		JmaIntensity.Int1 => new(Int1Foreground, Int1Background, Int1Border),
		JmaIntensity.Int2 => new(Int2Foreground, Int2Background, Int2Border),
		JmaIntensity.Int3 => new(Int3Foreground, Int3Background, Int3Border),
		JmaIntensity.Int4 => new(Int4Foreground, Int4Background, Int4Border),
		JmaIntensity.Int5Lower => new(Int5LowerForeground, Int5LowerBackground, Int5LowerBorder),
		JmaIntensity.Int5Upper => new(Int5UpperForeground, Int5UpperBackground, Int5UpperBorder),
		JmaIntensity.Int6Lower => new(Int6LowerForeground, Int6LowerBackground, Int6LowerBorder),
		JmaIntensity.Int6Upper => new(Int6UpperForeground, Int6UpperBackground, Int6UpperBorder),
		JmaIntensity.Int7 => new(Int7Foreground, Int7Background, Int7Border),
		_ => throw new ArgumentOutOfRangeException(nameof(intensity))
	};

	[JsonIgnore]
	public IntensityColor this[LpgmIntensity intensity] => intensity switch
	{
		LpgmIntensity.Unknown => new(LpgmUnknownForeground, LpgmUnknownBackground, LpgmUnknownBorder),
		LpgmIntensity.Error => new(LpgmErrorForeground, LpgmErrorBackground, LpgmErrorBorder),
		LpgmIntensity.LpgmInt0 => new(LpgmInt0Foreground, LpgmInt0Background, LpgmInt0Border),
		LpgmIntensity.LpgmInt1 => new(LpgmInt1Foreground, LpgmInt1Background, LpgmInt1Border),
		LpgmIntensity.LpgmInt2 => new(LpgmInt2Foreground, LpgmInt2Background, LpgmInt2Border),
		LpgmIntensity.LpgmInt3 => new(LpgmInt3Foreground, LpgmInt3Background, LpgmInt3Border),
		LpgmIntensity.LpgmInt4 => new(LpgmInt4Foreground, LpgmInt4Background, LpgmInt4Border),
		_ => throw new ArgumentOutOfRangeException(nameof(intensity))
	};

	public record IntensityColor(string Foreground, string Background, string Border);

	public IntensityTheme Clone() => new()
	{
		Name = Name,
		BorderWidthMultiply = BorderWidthMultiply,
		// JmaIntensity colors
		UnknownForeground = UnknownForeground,
		UnknownBackground = UnknownBackground,
		UnknownBorder = UnknownBorder,
		ErrorForeground = ErrorForeground,
		ErrorBackground = ErrorBackground,
		ErrorBorder = ErrorBorder,
		Int0Foreground = Int0Foreground,
		Int0Background = Int0Background,
		Int0Border = Int0Border,
		Int1Foreground = Int1Foreground,
		Int1Background = Int1Background,
		Int1Border = Int1Border,
		Int2Foreground = Int2Foreground,
		Int2Background = Int2Background,
		Int2Border = Int2Border,
		Int3Foreground = Int3Foreground,
		Int3Background = Int3Background,
		Int3Border = Int3Border,
		Int4Foreground = Int4Foreground,
		Int4Background = Int4Background,
		Int4Border = Int4Border,
		Int5LowerForeground = Int5LowerForeground,
		Int5LowerBackground = Int5LowerBackground,
		Int5LowerBorder = Int5LowerBorder,
		Int5UpperForeground = Int5UpperForeground,
		Int5UpperBackground = Int5UpperBackground,
		Int5UpperBorder = Int5UpperBorder,
		Int6LowerForeground = Int6LowerForeground,
		Int6LowerBackground = Int6LowerBackground,
		Int6LowerBorder = Int6LowerBorder,
		Int6UpperForeground = Int6UpperForeground,
		Int6UpperBackground = Int6UpperBackground,
		Int6UpperBorder = Int6UpperBorder,
		Int7Foreground = Int7Foreground,
		Int7Background = Int7Background,
		Int7Border = Int7Border,
		// LpgmIntensity colors
		LpgmUnknownForeground = LpgmUnknownForeground,
		LpgmUnknownBackground = LpgmUnknownBackground,
		LpgmUnknownBorder = LpgmUnknownBorder,
		LpgmErrorForeground = LpgmErrorForeground,
		LpgmErrorBackground = LpgmErrorBackground,
		LpgmErrorBorder = LpgmErrorBorder,
		LpgmInt0Foreground = LpgmInt0Foreground,
		LpgmInt0Background = LpgmInt0Background,
		LpgmInt0Border = LpgmInt0Border,
		LpgmInt1Foreground = LpgmInt1Foreground,
		LpgmInt1Background = LpgmInt1Background,
		LpgmInt1Border = LpgmInt1Border,
		LpgmInt2Foreground = LpgmInt2Foreground,
		LpgmInt2Background = LpgmInt2Background,
		LpgmInt2Border = LpgmInt2Border,
		LpgmInt3Foreground = LpgmInt3Foreground,
		LpgmInt3Background = LpgmInt3Background,
		LpgmInt3Border = LpgmInt3Border,
		LpgmInt4Foreground = LpgmInt4Foreground,
		LpgmInt4Background = LpgmInt4Background,
		LpgmInt4Border = LpgmInt4Border,
	};

	public ResourceDictionary CreateResourceDictionary()
	{
		Color GetColor(string colorString, string fallbackColorString)
		{
			if (Color.TryParse(colorString, out var color))
				return color;
			// フォールバックさせる
			return Color.TryParse(fallbackColorString, out var fallback) ? fallback : Colors.Black;
		}

		var dict = new ResourceDictionary();
		foreach (var i in Enum.GetValues<JmaIntensity>())
		{
			var intensityColor = this[i];
			var standardColor = Standard[i];
			dict.Add($"{i}Foreground", GetColor(intensityColor.Foreground, standardColor.Foreground));
			dict.Add($"{i}Background", GetColor(intensityColor.Background, standardColor.Background));
			dict.Add($"{i}Border", GetColor(intensityColor.Border, standardColor.Border));
		}
		foreach (var i in Enum.GetValues<LpgmIntensity>())
		{
			if (i is LpgmIntensity.Unknown or LpgmIntensity.Error)
				continue;
			var intensityColor = this[i];
			var standardColor = Standard[i];
			dict.Add($"{i}Foreground", GetColor(intensityColor.Foreground, standardColor.Foreground));
			dict.Add($"{i}Background", GetColor(intensityColor.Background, standardColor.Background));
			dict.Add($"{i}Border", GetColor(intensityColor.Border, standardColor.Border));
		}
		dict.Add("BorderWidthMultiply", BorderWidthMultiply);
		return dict;
	}

	public static IntensityTheme Standard { get; } = new()
	{
		Name = "Standard",
		BorderWidthMultiply = 0.125f,
		// JmaIntensity colors
		UnknownForeground = "#000000", UnknownBackground = "#808080", UnknownBorder = "#CCCCCC",
		ErrorForeground = "#b30f20", ErrorBackground = "#ffff6c", ErrorBorder = "#FFFF52",
		Int0Foreground = "#E6E6E6", Int0Background = "#808080", Int0Border = "#CCCCCC",
		Int1Foreground = "#000000", Int1Background = "#51b3fc", Int1Border = "#9DD3FC",
		Int2Foreground = "#000000", Int2Background = "#7dd45a", Int2Border = "#ACED93",
		Int3Foreground = "#000000", Int3Background = "#f0de7e", Int3Border = "#F0EAC5",
		Int4Foreground = "#000000", Int4Background = "#fa782c", Int4Border = "#FAA878",
		Int5LowerForeground = "#FFFFFF", Int5LowerBackground = "#b30f20", Int5LowerBorder = "#CC4E5B",
		Int5UpperForeground = "#FFFFFF", Int5UpperBackground = "#b30f20", Int5UpperBorder = "#CC4E5B",
		Int6LowerForeground = "#b30f20", Int6LowerBackground = "#ffcdde", Int6LowerBorder = "#FFFFFF",
		Int6UpperForeground = "#b30f20", Int6UpperBackground = "#ffcdde", Int6UpperBorder = "#FFFFFF",
		Int7Foreground = "#FFFFFF", Int7Background = "#9400d3", Int7Border = "#ffff6c",
		// LpgmIntensity colors
		LpgmUnknownForeground = "#000000", LpgmUnknownBackground = "#808080", LpgmUnknownBorder = "#CCCCCC",
		LpgmErrorForeground = "#b30f20", LpgmErrorBackground = "#ffff6c", LpgmErrorBorder = "#FFFF52",
		LpgmInt0Foreground = "#E6E6E6", LpgmInt0Background = "#808080", LpgmInt0Border = "#CCCCCC",
		LpgmInt1Foreground = "#000000", LpgmInt1Background = "#17BBD6", LpgmInt1Border = "#58C5D6",
		LpgmInt2Foreground = "#000000", LpgmInt2Background = "#F0FF09", LpgmInt2Border = "#FAFFB2",
		LpgmInt3Foreground = "#FFFFFF", LpgmInt3Background = "#D64700", LpgmInt3Border = "#F08048",
		LpgmInt4Foreground = "#FFFFFF", LpgmInt4Background = "#990033", LpgmInt4Border = "#B2365F",
	};

	public static IntensityTheme Jma { get; } = new()
	{
		Name = "JMA",
		BorderWidthMultiply = 0.05f,
		// JmaIntensity colors
		UnknownForeground = "#000000", UnknownBackground = "#808080", UnknownBorder = "#000000",
		ErrorForeground = "#b30f20", ErrorBackground = "#ffff6c", ErrorBorder = "#000000",
		Int0Foreground = "#FFFFFF", Int0Background = "#808080", Int0Border = "#000000",
		Int1Foreground = "#000000", Int1Background = "#F2F2FF", Int1Border = "#000000",
		Int2Foreground = "#000000", Int2Background = "#00AAFF", Int2Border = "#000000",
		Int3Foreground = "#FFFFFF", Int3Background = "#0041FF", Int3Border = "#000000",
		Int4Foreground = "#000000", Int4Background = "#FAE696", Int4Border = "#000000",
		Int5LowerForeground = "#000000", Int5LowerBackground = "#FFE600", Int5LowerBorder = "#000000",
		Int5UpperForeground = "#000000", Int5UpperBackground = "#FF9900", Int5UpperBorder = "#000000",
		Int6LowerForeground = "#FFFFFF", Int6LowerBackground = "#FF2800", Int6LowerBorder = "#000000",
		Int6UpperForeground = "#FFFFFF", Int6UpperBackground = "#A50021", Int6UpperBorder = "#000000",
		Int7Foreground = "#FFFFFF", Int7Background = "#B40068", Int7Border = "#000000",
		// LpgmIntensity colors
		LpgmUnknownForeground = "#000000", LpgmUnknownBackground = "#808080", LpgmUnknownBorder = "#000000",
		LpgmErrorForeground = "#b30f20", LpgmErrorBackground = "#ffff6c", LpgmErrorBorder = "#000000",
		LpgmInt0Foreground = "#FFFFFF", LpgmInt0Background = "#808080", LpgmInt0Border = "#000000",
		LpgmInt1Foreground = "#FFFFFF", LpgmInt1Background = "#0041FF", LpgmInt1Border = "#000000",
		LpgmInt2Foreground = "#000000", LpgmInt2Background = "#FFE600", LpgmInt2Border = "#000000",
		LpgmInt3Foreground = "#FFFFFF", LpgmInt3Background = "#FF2800", LpgmInt3Border = "#000000",
		LpgmInt4Foreground = "#FFFFFF", LpgmInt4Background = "#A50021", LpgmInt4Border = "#000000",
	};

	public static IntensityTheme Quarog { get; } = new()
	{
		Name = "Quarog",
		BorderWidthMultiply = 0.125f,
		// JmaIntensity colors
		UnknownForeground = "#F5FFFFFF", UnknownBackground = "#1e2832", UnknownBorder = "#171F27",
		ErrorForeground = "#F5FFFFFF", ErrorBackground = "#5a646e", ErrorBorder = "#4F5860",
		Int0Foreground = "#F5a0aab4", Int0Background = "#5a646e", Int0Border = "#4F5860",
		Int1Foreground = "#F5FFFFFF", Int1Background = "#325a8c", Int1Border = "#294A73",
		Int2Foreground = "#F5FFFFFF", Int2Background = "#3278d2", Int2Border = "#2C69B8",
		Int3Foreground = "#F51E2832", Int3Background = "#32d2e6", Int3Border = "#2CBBCC",
		Int4Foreground = "#F51e2832", Int4Background = "#fafa8c", Int4Border = "#E0E07E",
		Int5LowerForeground = "#F51e2832", Int5LowerBackground = "#fabe32", Int5LowerBorder = "#E0AB2D",
		Int5UpperForeground = "#F51e2832", Int5UpperBackground = "#fa821e", Int5UpperBorder = "#E0751B",
		Int6LowerForeground = "#F5FFFFFF", Int6LowerBackground = "#e61414", Int6LowerBorder = "#CC1212",
		Int6UpperForeground = "#F5FFFFFF", Int6UpperBackground = "#a01432", Int6UpperBorder = "#86112A",
		Int7Foreground = "#F5FFFFFF", Int7Background = "#5a1446", Int7Border = "#400E32",
		// LpgmIntensity colors
		LpgmUnknownForeground = "#F5FFFFFF", LpgmUnknownBackground = "#1e2832", LpgmUnknownBorder = "#171F27",
		LpgmErrorForeground = "#F5FFFFFF", LpgmErrorBackground = "#5a646e", LpgmErrorBorder = "#4F5860",
		LpgmInt0Foreground = "#F5a0aab4", LpgmInt0Background = "#5a646e", LpgmInt0Border = "#4F5860",
		LpgmInt1Foreground = "#F51E2832", LpgmInt1Background = "#32d2e6", LpgmInt1Border = "#2CBBCC",
		LpgmInt2Foreground = "#F51e2832", LpgmInt2Background = "#fabe32", LpgmInt2Border = "#E0AB2D",
		LpgmInt3Foreground = "#F5FFFFFF", LpgmInt3Background = "#e61414", LpgmInt3Border = "#CC1212",
		LpgmInt4Foreground = "#F5FFFFFF", LpgmInt4Background = "#a01432", LpgmInt4Border = "#86112A",
	};

	public static IntensityTheme Vivid { get; } = new()
	{
		Name = "Vivid",
		BorderWidthMultiply = 0.05f,
		// JmaIntensity colors
		UnknownForeground = "#000000", UnknownBackground = "#808080", UnknownBorder = "#000000",
		ErrorForeground = "#b30f20", ErrorBackground = "#ffff6c", ErrorBorder = "#000000",
		Int0Foreground = "#FFFFFF", Int0Background = "#808080", Int0Border = "#000000",
		Int1Foreground = "#a9a9a9", Int1Background = "#fffafa", Int1Border = "#000000",
		Int2Foreground = "#FFFFFF", Int2Background = "#1e90ff", Int2Border = "#000000",
		Int3Foreground = "#FFFFFF", Int3Background = "#3cb371", Int3Border = "#000000",
		Int4Foreground = "#FFFFFF", Int4Background = "#daa520", Int4Border = "#000000",
		Int5LowerForeground = "#FFFFFF", Int5LowerBackground = "#ff8c00", Int5LowerBorder = "#000000",
		Int5UpperForeground = "#FFFFFF", Int5UpperBackground = "#ff8c00", Int5UpperBorder = "#000000",
		Int6LowerForeground = "#FFFFFF", Int6LowerBackground = "#dc143c", Int6LowerBorder = "#000000",
		Int6UpperForeground = "#FFFFFF", Int6UpperBackground = "#dc143c", Int6UpperBorder = "#000000",
		Int7Foreground = "#FFFFFF", Int7Background = "#ff1493", Int7Border = "#000000",
		// LpgmIntensity colors
		LpgmUnknownForeground = "#000000", LpgmUnknownBackground = "#808080", LpgmUnknownBorder = "#000000",
		LpgmErrorForeground = "#b30f20", LpgmErrorBackground = "#ffff6c", LpgmErrorBorder = "#000000",
		LpgmInt0Foreground = "#E6E6E6", LpgmInt0Background = "#808080", LpgmInt0Border = "#CCCCCC",
		LpgmInt1Foreground = "#000000", LpgmInt1Background = "#17BBD6", LpgmInt1Border = "#58C5D6",
		LpgmInt2Foreground = "#000000", LpgmInt2Background = "#F0FF09", LpgmInt2Border = "#FAFFB2",
		LpgmInt3Foreground = "#FFFFFF", LpgmInt3Background = "#D64700", LpgmInt3Border = "#F08048",
		LpgmInt4Foreground = "#FFFFFF", LpgmInt4Background = "#990033", LpgmInt4Border = "#B2365F",
	};

	public static IntensityTheme[] DefaultThemes { get; } = [Standard, Vivid, Quarog, Jma];
}
