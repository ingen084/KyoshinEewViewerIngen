using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Core.Models;

public class WindowTheme
{
	public required string Name { get; init; }
	/// <summary>
	/// ウィンドウのタイトルバーの背景色
	/// </summary>
	public required string TitleBackgroundColor { get; init; }
	/// <summary>
	/// ボタンなどのスタイルをダークテーマ調にするか
	/// </summary>
	public required bool IsDark { get; init; }

	/// <summary>
	/// 地図配色 海外地形(ボーダーは設定不可)
	/// </summary>
	public required string OverseasLandColor { get; init; }
	/// <summary>
	/// 地図配色 地形
	/// </summary>
	public required string LandColor { get; init; }
	/// <summary>
	/// 地図配色 海岸線
	/// </summary>
	public required string LandStrokeColor { get; init; }
	/// <summary>
	/// 地図配色 海岸線の太さ
	/// 0 にすることで軽量化できる
	/// </summary>
	public required float LandStrokeThickness { get; init; }
	/// <summary>
	/// 地図配色 都道府県境界線
	/// </summary>
	public required string PrefStrokeColor { get; init; }
	/// <summary>
	/// 地図配色 都道府県境界線の太さ
	/// </summary>
	public required float PrefStrokeThickness { get; init; }
	/// <summary>
	/// 地図配色 地域境界線
	/// </summary>
	public required string AreaStrokeColor { get; init; }
	/// <summary>
	/// 地図配色 地域境界線の太さ
	/// </summary>
	public required float AreaStrokeThickness { get; init; }

	/// <summary>
	/// メイン背景色
	/// </summary>
	public required string MainBackgroundColor { get; init; }
	/// <summary>
	/// メイン文字色
	/// </summary>
	public required string ForegroundColor { get; init; }
	/// <summary>
	/// サブ文字色(補足等)
	/// </summary>
	public required string SubForegroundColor { get; init; }
	/// <summary>
	/// 強調文字(現状では強震モニタリプレイ時の時刻色)
	/// </summary>
	public required string EmphasisForegroundColor { get; init; }

	/// <summary>
	/// ドック(要素ウィンドウ)背景色
	/// </summary>
	public required string DockBackgroundColor { get; init; }
	/// <summary>
	/// ドック(要素ウィンドウ)タイトル部分背景色
	/// </summary>
	public required string DockTitleBackgroundColor { get; init; }
	/// <summary>
	/// ドックエラー･警告配色背景色
	/// </summary>
	public required string DockWarningBackgroundColor { get; init; }
	/// <summary>
	/// ドックエラー･警告配色タイトル部分背景色
	/// </summary>
	public required string DockWarningTitleBackgroundColor { get; init; }

	/// <summary>
	/// エラー･警告文字色
	/// </summary>
	public required string WarningForegroundColor { get; init; }
	/// <summary>
	/// エラー･警告サブ文字色
	/// </summary>
	public required string WarningSubForegroundColor { get; init; }
	/// <summary>
	/// エラー･警告背景色
	/// </summary>
	public required string WarningBackgroundColor { get; init; }

	/// <summary>
	/// 津波予報色
	/// </summary>
	public required string TsunamiForecastColor { get; init; }
	/// <summary>
	/// 津波予報文字色
	/// </summary>
	public required string TsunamiForecastForegroundColor { get; init; }
	/// <summary>
	/// 津波注意報色
	/// </summary>
	public required string TsunamiAdvisoryColor { get; init; }
	/// <summary>
	/// 津波注意報文字色
	/// </summary>
	public required string TsunamiAdvisoryForegroundColor { get; init; }
	/// <summary>
	/// 津波警報色
	/// </summary>
	public required string TsunamiWarningColor { get; init; }
	/// <summary>
	/// 津波警報文字色
	/// </summary>
	public required string TsunamiWarningForegroundColor { get; init; }
	/// <summary>
	/// 津波大津波警報色
	/// </summary>
	public required string TsunamiMajorWarningColor { get; init; }
	/// <summary>
	/// 津波大津波警報文字色
	/// </summary>
	public required string TsunamiMajorWarningForegroundColor { get; init; }

	/// <summary>
	/// 震央アイコンボーダー色(地震情報)
	/// </summary>
	public required string EarthquakeHypocenterBorderColor { get; init; }
	/// <summary>
	/// 震央アイコン中央色(地震情報)
	/// </summary>
	public required string EarthquakeHypocenterColor { get; init; }

	/// <summary>
	/// 震央アイコンボーダー色(緊急地震速報 予報)
	/// </summary>
	public required string EewForecastHypocenterBorderColor { get; init; }
	/// <summary>
	/// 震央アイコン中央色(緊急地震速報 予報)
	/// </summary>
	public required string EewForecastHypocenterColor { get; init; }

	/// <summary>
	/// 震央アイコンボーダー色(緊急地震速報 警報)
	/// </summary>
	public required string EewWarningHypocenterBorderColor { get; init; }
	/// <summary>
	/// 震央アイコン中央色(緊急地震速報 警報)
	/// </summary>
	public required string EewWarningHypocenterColor { get; init; }

	/// <summary>
	/// 緊急地震速報震央アイコンの点滅アニメーションを有効にするか
	/// </summary>
	public required bool IsEewHypocenterBlinkAnimation { get; init; }

	/// <summary>
	/// 緊急地震速報(予報)P波色
	/// </summary>
	public required string EewForecastPWaveColor { get; init; }
	/// <summary>
	/// 緊急地震速報(予報)S波色
	/// </summary>
	public required string EewForecastSWaveColor { get; init; }
	/// <summary>
	/// 緊急地震速報(予報)のS波色をグラデーションにするか
	/// </summary>
	public required bool IsEewForecastSWaveGradient { get; init; }
	/// <summary>
	/// 緊急地震速報(警報)P波色
	/// </summary>
	public required string EewWarningPWaveColor { get; init; }
	/// <summary>
	/// 緊急地震速報(警報)S波色
	/// </summary>
	public required string EewWarningSWaveColor { get; init; }
	/// <summary>
	/// 緊急地震速報(警報)のS波色をグラデーションにするか
	/// </summary>
	public required bool IsEewWarningSWaveGradient { get; init; }

	public ResourceDictionary CreateResourceDictionary()
	{
		Color GetColor(Func<WindowTheme, string> propertySelector)
		{
			if (Color.TryParse(propertySelector(this), out var color))
				return color;
			// IsDark に応じてフォールバックさせる
			// ここでのエラーは検知させるためなにもしない
			return Color.Parse(propertySelector(IsDark ? Dark : Light));
		}

		return new ResourceDictionary
		{
			{ "IsDarkTheme", IsDark },

			{ "TitleBackgroundColor", GetColor( x => x.TitleBackgroundColor) },

			{ "OverseasLandColor", GetColor( x => x.OverseasLandColor) },
			{ "LandColor", GetColor( x => x.LandColor) },
			{ "LandStrokeColor", GetColor( x => x.LandStrokeColor) },
			{ "LandStrokeThickness", LandStrokeThickness },
			{ "PrefStrokeColor", GetColor( x => x.PrefStrokeColor) },
			{ "PrefStrokeThickness", PrefStrokeThickness },
			{ "AreaStrokeColor", GetColor( x => x.AreaStrokeColor) },
			{ "AreaStrokeThickness", AreaStrokeThickness },

			{ "MainBackgroundColor", GetColor( x => x.MainBackgroundColor) },
			{ "ForegroundColor", GetColor( x => x.ForegroundColor) },
			{ "SubForegroundColor", GetColor( x => x.SubForegroundColor) },
			{ "EmphasisForegroundColor", GetColor( x => x.EmphasisForegroundColor) },
			{ "DockBackgroundColor", GetColor( x => x.DockBackgroundColor) },
			{ "DockTitleBackgroundColor", GetColor( x => x.DockTitleBackgroundColor) },
			{ "DockWarningBackgroundColor", GetColor( x => x.DockWarningBackgroundColor) },
			{ "DockWarningTitleBackgroundColor", GetColor( x => x.DockWarningTitleBackgroundColor) },
			{ "WarningForegroundColor", GetColor( x => x.WarningForegroundColor) },
			{ "WarningSubForegroundColor", GetColor( x => x.WarningSubForegroundColor) },
			{ "WarningBackgroundColor", GetColor( x => x.WarningBackgroundColor) },
			{ "TsunamiForecastColor", GetColor( x => x.TsunamiForecastColor) },
			{ "TsunamiForecastForegroundColor", GetColor( x => x.TsunamiForecastForegroundColor) },
			{ "TsunamiAdvisoryColor", GetColor( x => x.TsunamiAdvisoryColor) },
			{ "TsunamiAdvisoryForegroundColor", GetColor( x => x.TsunamiAdvisoryForegroundColor) },
			{ "TsunamiWarningColor", GetColor( x => x.TsunamiWarningColor) },
			{ "TsunamiWarningForegroundColor", GetColor( x => x.TsunamiWarningForegroundColor) },
			{ "TsunamiMajorWarningColor", GetColor( x => x.TsunamiMajorWarningColor) },
			{ "TsunamiMajorWarningForegroundColor", GetColor( x => x.TsunamiMajorWarningForegroundColor) },
			{ "EarthquakeHypocenterBorderColor", GetColor( x => x.EarthquakeHypocenterBorderColor) },
			{ "EarthquakeHypocenterColor", GetColor( x => x.EarthquakeHypocenterColor) },
			{ "EewForecastHypocenterBorderColor", GetColor( x => x.EewForecastHypocenterBorderColor) },
			{ "EewForecastHypocenterColor", GetColor( x => x.EewForecastHypocenterColor) },
			{ "EewWarningHypocenterBorderColor", GetColor( x => x.EewWarningHypocenterBorderColor) },
			{ "EewWarningHypocenterColor", GetColor( x => x.EewWarningHypocenterColor) },
			{ "IsEewHypocenterBlinkAnimation", IsEewHypocenterBlinkAnimation },
			{ "EewForecastPWaveColor", GetColor( x => x.EewForecastPWaveColor) },
			{ "EewForecastSWaveColor", GetColor( x => x.EewForecastSWaveColor) },
			{ "IsEewForecastSWaveGradient", IsEewForecastSWaveGradient },
			{ "EewWarningPWaveColor", GetColor( x => x.EewWarningPWaveColor) },
			{ "EewWarningSWaveColor", GetColor( x => x.EewWarningSWaveColor) },
			{ "IsEewWarningSWaveGradient", IsEewWarningSWaveGradient },
		};
	}

	public static WindowTheme Dark { get; } = new()
	{
		Name = "Dark",
		IsDark = true,
		TitleBackgroundColor = "#505050",

		OverseasLandColor = "#FF2D2D30",
		LandColor = "#FF3E3E42",
		LandStrokeColor = "#A9A9A9",
		LandStrokeThickness = 0.6f,
		PrefStrokeColor = "#808080",
		PrefStrokeThickness = 0.6f,
		AreaStrokeColor = "#696969",
		AreaStrokeThickness = 0.4f,

		MainBackgroundColor = "#FF1E1E1E",
		ForegroundColor = "#FAFAFA",
		SubForegroundColor = "#D3D3D3",
		EmphasisForegroundColor = "#FFFF00",

		DockBackgroundColor = "#DD808080",
		DockTitleBackgroundColor = "#DD505050",
		DockWarningTitleBackgroundColor = "#BBAA0000",
		DockWarningBackgroundColor = "#BBFF0000",

		WarningForegroundColor = "#FFFF00",
		WarningSubForegroundColor = "#f0e68c",
		WarningBackgroundColor = "#BBFF0000",

		TsunamiForecastColor = "#008b8b",
		TsunamiForecastForegroundColor = "#FFFFFF",
		TsunamiAdvisoryColor = "#ffd700",
		TsunamiAdvisoryForegroundColor = "#000000",
		TsunamiWarningColor = "#dc143c",
		TsunamiWarningForegroundColor = "#FFFFFF",
		TsunamiMajorWarningColor = "#9400d3",
		TsunamiMajorWarningForegroundColor = "#FFFFFF",

		EarthquakeHypocenterBorderColor = "#FFFF00",
		EarthquakeHypocenterColor = "#FF0000",

		EewForecastHypocenterBorderColor = "#FFFF00",
		EewForecastHypocenterColor = "#dc143c",

		EewWarningHypocenterBorderColor = "#FFFF00",
		EewWarningHypocenterColor = "#FF0000",

		IsEewHypocenterBlinkAnimation = true,

		EewForecastPWaveColor = "#C800A0FF",
		EewForecastSWaveColor = "#FF5078",
		IsEewForecastSWaveGradient = true,

		EewWarningPWaveColor = "#C800A0FF",
		EewWarningSWaveColor = "#FF5078",
		IsEewWarningSWaveGradient = true,
	};

	public static WindowTheme Light { get; } = new()
	{
		Name = "Light",
		IsDark = false,
		TitleBackgroundColor = "#FFFFFF",

		OverseasLandColor = "#a9a9a9",
		LandColor = "#FFF2EFE9",
		LandStrokeColor = "#FF6E788C",
		LandStrokeThickness = 0.0f,
		PrefStrokeColor = "#FFAAA3CE",
		PrefStrokeThickness = 0.5f,
		AreaStrokeColor = "#FFAAA3CE",
		AreaStrokeThickness = 0.3f,

		MainBackgroundColor = "#FFAAD3DF",
		ForegroundColor = "#191970",
		SubForegroundColor = "#FF444444",
		EmphasisForegroundColor = "#b8860b",

		DockBackgroundColor = "#DDDDDDDD",
		DockTitleBackgroundColor = "#DDFFFFFF",
		DockWarningTitleBackgroundColor = "#DDAA0000",
		DockWarningBackgroundColor = "#DDFF0000",

		WarningForegroundColor = "#FFFF00",
		WarningSubForegroundColor = "#f0e68c",
		WarningBackgroundColor = "#EEFF0000",

		TsunamiForecastColor = "#008b8b",
		TsunamiForecastForegroundColor = "#FFFFFF",
		TsunamiAdvisoryColor = "#ffa500",
		TsunamiAdvisoryForegroundColor = "#000000",
		TsunamiWarningColor = "#dc143c",
		TsunamiWarningForegroundColor = "#FFFFFF",
		TsunamiMajorWarningColor = "#9400d3",
		TsunamiMajorWarningForegroundColor = "#FFFFFF",

		EarthquakeHypocenterBorderColor = "#FFFF00",
		EarthquakeHypocenterColor = "#FF0000",

		EewForecastHypocenterBorderColor = "#FFFF00",
		EewForecastHypocenterColor = "#dc143c",

		EewWarningHypocenterBorderColor = "#FFFF00",
		EewWarningHypocenterColor = "#FF0000",

		IsEewHypocenterBlinkAnimation = true,

		EewForecastPWaveColor = "#C800A0FF",
		EewForecastSWaveColor = "#FF5078",
		IsEewForecastSWaveGradient = true,

		EewWarningPWaveColor = "#C800A0FF",
		EewWarningSWaveColor = "#FF5078",
		IsEewWarningSWaveGradient = true,
	};

	public static WindowTheme Quarog { get; } = new()
	{
		Name = "Quarog",
		IsDark = true,
		TitleBackgroundColor = "#1e2832",

		OverseasLandColor = "#32465A",
		LandColor = "#506478",
		LandStrokeColor = "#8CA0B4",
		LandStrokeThickness = 0.8f,
		PrefStrokeColor = "#8CA0B4",
		PrefStrokeThickness = 0.8f,
		AreaStrokeColor = "#8CA0B4",
		AreaStrokeThickness = 0.4f,

		MainBackgroundColor = "#14283C",
		ForegroundColor = "#FFFFFF",
		SubForegroundColor = "#FFFFFF",
		EmphasisForegroundColor = "#fafa8c",

		DockBackgroundColor = "#EE3C4650",
		DockTitleBackgroundColor = "#EE505A64",
		DockWarningTitleBackgroundColor = "#EEb4321e",
		DockWarningBackgroundColor = "#EE37221f",

		WarningForegroundColor = "#fafa8c",
		WarningSubForegroundColor = "#fafa8c",
		WarningBackgroundColor = "#EE4b3835",

		TsunamiForecastColor = "#508CA0",
		TsunamiForecastForegroundColor = "#FFFFFF",
		TsunamiAdvisoryColor = "#F0DC28",
		TsunamiAdvisoryForegroundColor = "#1E2832",
		TsunamiWarningColor = "#DC2800",
		TsunamiWarningForegroundColor = "#FFFFFF",
		TsunamiMajorWarningColor = "#BE00F0",
		TsunamiMajorWarningForegroundColor = "#FFFFFF",

		EarthquakeHypocenterBorderColor = "#FFFFFF",
		EarthquakeHypocenterColor = "#e65a5a",

		EewForecastHypocenterBorderColor = "#FFFFFF",
		EewForecastHypocenterColor = "#e65a5a",

		EewWarningHypocenterBorderColor = "#FFFFFF",
		EewWarningHypocenterColor = "#e65a5a",

		IsEewHypocenterBlinkAnimation = false,

		EewForecastPWaveColor = "#50a0fa",
		EewForecastSWaveColor = "#e65a5a",
		IsEewForecastSWaveGradient = true,

		EewWarningPWaveColor = "#50a0fa",
		EewWarningSWaveColor = "#e65a5a",
		IsEewWarningSWaveGradient = true,
	};

	public static WindowTheme[] DefaultThemes { get; } = [
		Dark,
		Light,
		Quarog,
	];
}
