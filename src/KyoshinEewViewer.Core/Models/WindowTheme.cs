using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace KyoshinEewViewer.Core.Models;

public partial class WindowTheme : ObservableObject
{
	[ObservableProperty]
	public required partial string Name { get; set; } = "";

	/// <summary>
	/// ウィンドウのタイトルバーの背景色
	/// </summary>
	[ObservableProperty]
	public partial string TitleBackgroundColor { get; set; } = "";

	/// <summary>
	/// ボタンなどのスタイルをダークテーマ調にするか
	/// </summary>
	[ObservableProperty]
	public partial bool IsDark { get; set; }

	/// <summary>
	/// 地図配色 海外地形(ボーダーは設定不可)
	/// </summary>
	[ObservableProperty]
	public partial string OverseasLandColor { get; set; } = "";

	/// <summary>
	/// 地図配色 地形
	/// </summary>
	[ObservableProperty]
	public partial string LandColor { get; set; } = "";

	/// <summary>
	/// 地図配色 海岸線
	/// </summary>
	[ObservableProperty]
	public partial string LandStrokeColor { get; set; } = "";

	/// <summary>
	/// 地図配色 海岸線の太さ
	/// 0 にすることで軽量化できる
	/// </summary>
	[ObservableProperty]
	public partial double LandStrokeThickness { get; set; } = 0.6;

	/// <summary>
	/// 地図配色 都道府県境界線
	/// </summary>
	[ObservableProperty]
	public partial string PrefStrokeColor { get; set; } = "";

	/// <summary>
	/// 地図配色 都道府県境界線の太さ
	/// </summary>
	[ObservableProperty]
	public partial double PrefStrokeThickness { get; set; } = 0.6;

	/// <summary>
	/// 地図配色 地域境界線
	/// </summary>
	[ObservableProperty]
	public partial string AreaStrokeColor { get; set; } = "";

	/// <summary>
	/// 地図配色 地域境界線の太さ
	/// </summary>
	[ObservableProperty]
	public partial double AreaStrokeThickness { get; set; } = 0.4;

	/// <summary>
	/// メイン背景色
	/// </summary>
	[ObservableProperty]
	public partial string MainBackgroundColor { get; set; } = "";

	/// <summary>
	/// メイン文字色
	/// </summary>
	[ObservableProperty]
	public partial string ForegroundColor { get; set; } = "";

	/// <summary>
	/// サブ文字色(補足等)
	/// </summary>
	[ObservableProperty]
	public partial string SubForegroundColor { get; set; } = "";

	/// <summary>
	/// 強調文字(現状では強震モニタリプレイ時の時刻色)
	/// </summary>
	[ObservableProperty]
	public partial string EmphasisForegroundColor { get; set; } = "";


	/// <summary>
	/// ドック(要素ウィンドウ)背景色
	/// </summary>
	[ObservableProperty]
	public partial string DockBackgroundColor { get; set; } = "";

	/// <summary>
	/// ドック(要素ウィンドウ)タイトル部分背景色
	/// </summary>
	[ObservableProperty]
	public partial string DockTitleBackgroundColor { get; set; } = "";

	/// <summary>
	/// ドックエラー･警告配色背景色
	/// </summary>
	[ObservableProperty]
	public partial string DockWarningBackgroundColor { get; set; } = "";

	/// <summary>
	/// ドックエラー･警告配色タイトル部分背景色
	/// </summary>
	[ObservableProperty]
	public partial string DockWarningTitleBackgroundColor { get; set; } = "";

	/// <summary>
	/// エラー･警告文字色
	/// </summary>
	[ObservableProperty]
	public partial string WarningForegroundColor { get; set; } = "";

	/// <summary>
	/// エラー･警告サブ文字色
	/// </summary>
	[ObservableProperty]
	public partial string WarningSubForegroundColor { get; set; } = "";

	/// <summary>
	/// エラー･警告背景色
	/// </summary>
	[ObservableProperty]
	public partial string WarningBackgroundColor { get; set; } = "";

	/// <summary>
	/// 津波予報色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiForecastColor { get; set; } = "";

	/// <summary>
	/// 津波予報文字色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiForecastForegroundColor { get; set; } = "";

	/// <summary>
	/// 津波注意報色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiAdvisoryColor { get; set; } = "";

	/// <summary>
	/// 津波注意報文字色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiAdvisoryForegroundColor { get; set; } = "";

	/// <summary>
	/// 津波警報色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiWarningColor { get; set; } = "";

	/// <summary>
	/// 津波警報文字色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiWarningForegroundColor { get; set; } = "";

	/// <summary>
	/// 大津波警報色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiMajorWarningColor { get; set; } = "";

	/// <summary>
	/// 大津波警報文字色
	/// </summary>
	[ObservableProperty]
	public partial string TsunamiMajorWarningForegroundColor { get; set; } = "";

	/// <summary>
	/// 震央アイコンボーダー色(地震情報)
	/// </summary>
	[ObservableProperty]
	public partial string EarthquakeHypocenterBorderColor { get; set; } = "";

	/// <summary>
	/// 震央アイコン中央色(地震情報)
	/// </summary>
	[ObservableProperty]
	public partial string EarthquakeHypocenterColor { get; set; } = "";

	/// <summary>
	/// 震央アイコンボーダー色(緊急地震速報 予報)
	/// </summary>
	[ObservableProperty]
	public partial string EewForecastHypocenterBorderColor { get; set; } = "";

	/// <summary>
	/// 震央アイコン中央色(緊急地震速報 予報)
	/// </summary>
	[ObservableProperty]
	public partial string EewForecastHypocenterColor { get; set; } = "";

	/// <summary>
	/// 震央アイコンボーダー色(緊急地震速報 警報)
	/// </summary>
	[ObservableProperty]
	public partial string EewWarningHypocenterBorderColor { get; set; } = "";

	/// <summary>
	/// 震央アイコン中央色(緊急地震速報 警報)
	/// </summary>
	[ObservableProperty]
	public partial string EewWarningHypocenterColor { get; set; } = "";

	/// <summary>
	/// 緊急地震速報震央アイコンの点滅アニメーションを有効にするか
	/// </summary>
	[ObservableProperty]
	public partial bool IsEewHypocenterBlinkAnimation { get; set; } = true;

	/// <summary>
	/// 緊急地震速報(予報)P波色
	/// </summary>
	[ObservableProperty]
	public partial string EewForecastPWaveColor { get; set; } = "";

	/// <summary>
	/// 緊急地震速報(予報)S波色
	/// </summary>
	[ObservableProperty]
	public partial string EewForecastSWaveColor { get; set; } = "";

	/// <summary>
	/// 緊急地震速報(予報)のS波色をグラデーションにするか
	/// </summary>
	[ObservableProperty]
	public partial bool IsEewForecastSWaveGradient { get; set; } = true;

	/// <summary>
	/// 緊急地震速報(警報)P波色
	/// </summary>
	[ObservableProperty]
	public partial string EewWarningPWaveColor { get; set; } = "";

	/// <summary>
	/// 緊急地震速報(警報)S波色
	/// </summary>
	[ObservableProperty]
	public partial string EewWarningSWaveColor { get; set; } = "";

	/// <summary>
	/// 緊急地震速報(警報)のS波色をグラデーションにするか
	/// </summary>
	[ObservableProperty]
	public partial bool IsEewWarningSWaveGradient { get; set; } = true;

	/// <summary>
	/// 降灰予報における『少量の降灰』
	/// </summary>
	[ObservableProperty]
	public partial string AshfallLight { get; set; } = "";

	/// <summary>
	/// 降灰予報における『少量の降灰』文字色
	/// </summary>
	[ObservableProperty]
	public partial string AshfallLightForeground { get; set; } = "";

	/// <summary>
	/// 降灰予報における『やや多量の降灰』
	/// </summary>
	[ObservableProperty]
	public partial string AshfallModerate { get; set; } = "";

	/// <summary>
	/// 降灰予報における『やや多量の降灰』文字色
	/// </summary>
	[ObservableProperty]
	public partial string AshfallModerateForeground { get; set; } = "";

	/// <summary>
	/// 降灰予報における『大量の降灰』
	/// </summary>
	[ObservableProperty]
	public partial string AshfallHeavy { get; set; } = "";

	/// <summary>
	/// 降灰予報における『大量の降灰』文字色
	/// </summary>
	[ObservableProperty]
	public partial string AshfallHeavyForeground { get; set; } = "";

	/// <summary>
	/// 降灰予報における『小さな噴石の落下』
	/// </summary>
	[ObservableProperty]
	public partial string SmallVolcanicBombFall { get; set; } = "";

	/// <summary>
	/// 降灰予報における『小さな噴石の落下』文字色
	/// </summary>
	[ObservableProperty]
	public partial string SmallVolcanicBombFallForeground { get; set; } = "";

	/// <summary>
	/// 気象 - 警戒レベル5（特別警報）
	/// </summary>
	[ObservableProperty]
	public partial string WeatherWarningLevel5Color { get; set; } = "";

	/// <summary>
	/// 気象 - 警戒レベル4（危険警報･土砂災害警戒情報）
	/// </summary>
	[ObservableProperty]
	public partial string WeatherWarningLevel4Color { get; set; } = "";

	/// <summary>
	/// 気象 - 警戒レベル3（警報）
	/// </summary>
	[ObservableProperty]
	public partial string WeatherWarningLevel3Color { get; set; } = "";

	/// <summary>
	/// 気象 - 警戒レベル2（注意報）
	/// </summary>
	[ObservableProperty]
	public partial string WeatherWarningLevel2Color { get; set; } = "";

	/// <summary>
	/// 海上警報 - 海上台風警報
	/// </summary>
	[ObservableProperty]
	public partial string MarineWarningTyphoonColor { get; set; } = "";

	/// <summary>
	/// 海上警報 - 海上暴風警報
	/// </summary>
	[ObservableProperty]
	public partial string MarineWarningStormColor { get; set; } = "";

	/// <summary>
	/// 海上警報 - 海上強風警報
	/// </summary>
	[ObservableProperty]
	public partial string MarineWarningGaleColor { get; set; } = "";

	/// <summary>
	/// 海上警報 - 海上風警報
	/// </summary>
	[ObservableProperty]
	public partial string MarineWarningWindColor { get; set; } = "";

	public WindowTheme Clone() => new()
	{
		Name = Name,
		IsDark = IsDark,
		TitleBackgroundColor = TitleBackgroundColor,
		OverseasLandColor = OverseasLandColor,
		LandColor = LandColor,
		LandStrokeColor = LandStrokeColor,
		LandStrokeThickness = LandStrokeThickness,
		PrefStrokeColor = PrefStrokeColor,
		PrefStrokeThickness = PrefStrokeThickness,
		AreaStrokeColor = AreaStrokeColor,
		AreaStrokeThickness = AreaStrokeThickness,
		MainBackgroundColor = MainBackgroundColor,
		ForegroundColor = ForegroundColor,
		SubForegroundColor = SubForegroundColor,
		EmphasisForegroundColor = EmphasisForegroundColor,
		DockBackgroundColor = DockBackgroundColor,
		DockTitleBackgroundColor = DockTitleBackgroundColor,
		DockWarningBackgroundColor = DockWarningBackgroundColor,
		DockWarningTitleBackgroundColor = DockWarningTitleBackgroundColor,
		WarningForegroundColor = WarningForegroundColor,
		WarningSubForegroundColor = WarningSubForegroundColor,
		WarningBackgroundColor = WarningBackgroundColor,
		TsunamiForecastColor = TsunamiForecastColor,
		TsunamiForecastForegroundColor = TsunamiForecastForegroundColor,
		TsunamiAdvisoryColor = TsunamiAdvisoryColor,
		TsunamiAdvisoryForegroundColor = TsunamiAdvisoryForegroundColor,
		TsunamiWarningColor = TsunamiWarningColor,
		TsunamiWarningForegroundColor = TsunamiWarningForegroundColor,
		TsunamiMajorWarningColor = TsunamiMajorWarningColor,
		TsunamiMajorWarningForegroundColor = TsunamiMajorWarningForegroundColor,
		EarthquakeHypocenterBorderColor = EarthquakeHypocenterBorderColor,
		EarthquakeHypocenterColor = EarthquakeHypocenterColor,
		EewForecastHypocenterBorderColor = EewForecastHypocenterBorderColor,
		EewForecastHypocenterColor = EewForecastHypocenterColor,
		EewWarningHypocenterBorderColor = EewWarningHypocenterBorderColor,
		EewWarningHypocenterColor = EewWarningHypocenterColor,
		IsEewHypocenterBlinkAnimation = IsEewHypocenterBlinkAnimation,
		EewForecastPWaveColor = EewForecastPWaveColor,
		EewForecastSWaveColor = EewForecastSWaveColor,
		IsEewForecastSWaveGradient = IsEewForecastSWaveGradient,
		EewWarningPWaveColor = EewWarningPWaveColor,
		EewWarningSWaveColor = EewWarningSWaveColor,
		IsEewWarningSWaveGradient = IsEewWarningSWaveGradient,
		AshfallLight = AshfallLight,
		AshfallLightForeground = AshfallLightForeground,
		AshfallModerate = AshfallModerate,
		AshfallModerateForeground = AshfallModerateForeground,
		AshfallHeavy = AshfallHeavy,
		AshfallHeavyForeground = AshfallHeavyForeground,
		SmallVolcanicBombFall = SmallVolcanicBombFall,
		SmallVolcanicBombFallForeground = SmallVolcanicBombFallForeground,
		WeatherWarningLevel5Color = WeatherWarningLevel5Color,
		WeatherWarningLevel4Color = WeatherWarningLevel4Color,
		WeatherWarningLevel3Color = WeatherWarningLevel3Color,
		WeatherWarningLevel2Color = WeatherWarningLevel2Color,
		MarineWarningTyphoonColor = MarineWarningTyphoonColor,
		MarineWarningStormColor = MarineWarningStormColor,
		MarineWarningGaleColor = MarineWarningGaleColor,
		MarineWarningWindColor = MarineWarningWindColor,
	};

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

			{ "TitleBackgroundColor", GetColor(x => x.TitleBackgroundColor) },

			{ "OverseasLandColor", GetColor(x => x.OverseasLandColor) },
			{ "LandColor", GetColor(x => x.LandColor) },
			{ "LandStrokeColor", GetColor(x => x.LandStrokeColor) },
			{ "LandStrokeThickness", LandStrokeThickness },
			{ "PrefStrokeColor", GetColor(x => x.PrefStrokeColor) },
			{ "PrefStrokeThickness", PrefStrokeThickness },
			{ "AreaStrokeColor", GetColor(x => x.AreaStrokeColor) },
			{ "AreaStrokeThickness", AreaStrokeThickness },

			{ "MainBackgroundColor", GetColor(x => x.MainBackgroundColor) },
			{ "MainForegroundColor", GetColor(x => x.ForegroundColor) },
			{ "SubForegroundColor", GetColor(x => x.SubForegroundColor) },
			{ "EmphasisForegroundColor", GetColor(x => x.EmphasisForegroundColor) },
			{ "DockBackgroundColor", GetColor(x => x.DockBackgroundColor) },
			{ "DockTitleBackgroundColor", GetColor(x => x.DockTitleBackgroundColor) },
			{ "DockWarningBackgroundColor", GetColor(x => x.DockWarningBackgroundColor) },
			{ "DockWarningTitleBackgroundColor", GetColor(x => x.DockWarningTitleBackgroundColor) },
			{ "WarningForegroundColor", GetColor(x => x.WarningForegroundColor) },
			{ "WarningSubForegroundColor", GetColor(x => x.WarningSubForegroundColor) },
			{ "WarningBackgroundColor", GetColor(x => x.WarningBackgroundColor) },
			{ "TsunamiForecastColor", GetColor(x => x.TsunamiForecastColor) },
			{ "TsunamiForecastForegroundColor", GetColor(x => x.TsunamiForecastForegroundColor) },
			{ "TsunamiAdvisoryColor", GetColor(x => x.TsunamiAdvisoryColor) },
			{ "TsunamiAdvisoryForegroundColor", GetColor(x => x.TsunamiAdvisoryForegroundColor) },
			{ "TsunamiWarningColor", GetColor(x => x.TsunamiWarningColor) },
			{ "TsunamiWarningForegroundColor", GetColor(x => x.TsunamiWarningForegroundColor) },
			{ "TsunamiMajorWarningColor", GetColor(x => x.TsunamiMajorWarningColor) },
			{ "TsunamiMajorWarningForegroundColor", GetColor(x => x.TsunamiMajorWarningForegroundColor) },
			{ "EarthquakeHypocenterBorderColor", GetColor(x => x.EarthquakeHypocenterBorderColor) },
			{ "EarthquakeHypocenterColor", GetColor(x => x.EarthquakeHypocenterColor) },
			{ "EewForecastHypocenterBorderColor", GetColor(x => x.EewForecastHypocenterBorderColor) },
			{ "EewForecastHypocenterColor", GetColor(x => x.EewForecastHypocenterColor) },
			{ "EewWarningHypocenterBorderColor", GetColor(x => x.EewWarningHypocenterBorderColor) },
			{ "EewWarningHypocenterColor", GetColor(x => x.EewWarningHypocenterColor) },
			{ "IsEewHypocenterBlinkAnimation", IsEewHypocenterBlinkAnimation },
			{ "EewForecastPWaveColor", GetColor(x => x.EewForecastPWaveColor) },
			{ "EewForecastSWaveColor", GetColor(x => x.EewForecastSWaveColor) },
			{ "IsEewForecastSWaveGradient", IsEewForecastSWaveGradient },
			{ "EewWarningPWaveColor", GetColor(x => x.EewWarningPWaveColor) },
			{ "EewWarningSWaveColor", GetColor(x => x.EewWarningSWaveColor) },
			{ "IsEewWarningSWaveGradient", IsEewWarningSWaveGradient },
			{ "AshfallLight", GetColor(x => x.AshfallLight) },
			{ "AshfallLightForeground", GetColor(x => x.AshfallLightForeground) },
			{ "AshfallModerate", GetColor(x => x.AshfallModerate) },
			{ "AshfallModerateForeground", GetColor(x => x.AshfallModerateForeground) },
			{ "AshfallHeavy", GetColor(x => x.AshfallHeavy) },
			{ "AshfallHeavyForeground", GetColor(x => x.AshfallHeavyForeground) },
			{ "SmallVolcanicBombFall", GetColor(x => x.SmallVolcanicBombFall) },
			{ "SmallVolcanicBombFallForeground", GetColor(x => x.SmallVolcanicBombFallForeground) },
			{ "WeatherWarningLevel5Color", GetColor(x => x.WeatherWarningLevel5Color) },
			{ "WeatherWarningLevel4Color", GetColor(x => x.WeatherWarningLevel4Color) },
			{ "WeatherWarningLevel3Color", GetColor(x => x.WeatherWarningLevel3Color) },
			{ "WeatherWarningLevel2Color", GetColor(x => x.WeatherWarningLevel2Color) },
			{ "MarineWarningTyphoonColor", GetColor(x => x.MarineWarningTyphoonColor) },
			{ "MarineWarningStormColor", GetColor(x => x.MarineWarningStormColor) },
			{ "MarineWarningGaleColor", GetColor(x => x.MarineWarningGaleColor) },
			{ "MarineWarningWindColor", GetColor(x => x.MarineWarningWindColor) },
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

		AshfallLight = "#a9a9a9",
		AshfallLightForeground = "#000000",
		AshfallModerate = "#808080",
		AshfallModerateForeground = "#000000",
		AshfallHeavy = "#696969",
		AshfallHeavyForeground = "#FFFFFF",
		SmallVolcanicBombFall = "#ff6666",
		SmallVolcanicBombFallForeground = "#000000",

		WeatherWarningLevel5Color = "#0C000C",
		WeatherWarningLevel4Color = "#AA00AA",
		WeatherWarningLevel3Color = "#FF2800",
		WeatherWarningLevel2Color = "#F2E700",

		MarineWarningTyphoonColor = "#80800080",
		MarineWarningStormColor = "#80DC143C",
		MarineWarningGaleColor = "#80FF8C00",
		MarineWarningWindColor = "#80FFD700",
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

		AshfallLight = "#a9a9a9",
		AshfallLightForeground = "#000000",
		AshfallModerate = "#808080",
		AshfallModerateForeground = "#000000",
		AshfallHeavy = "#696969",
		AshfallHeavyForeground = "#FFFFFF",
		SmallVolcanicBombFall = "#ff6666",
		SmallVolcanicBombFallForeground = "#000000",

		WeatherWarningLevel5Color = "#0C000C",
		WeatherWarningLevel4Color = "#AA00AA",
		WeatherWarningLevel3Color = "#FF2800",
		WeatherWarningLevel2Color = "#F2E700",

		MarineWarningTyphoonColor = "#80800080",
		MarineWarningStormColor = "#80DC143C",
		MarineWarningGaleColor = "#80FF8C00",
		MarineWarningWindColor = "#80FFD700",
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

		AshfallLight = "#a9a9a9",
		AshfallLightForeground = "#000000",
		AshfallModerate = "#808080",
		AshfallModerateForeground = "#000000",
		AshfallHeavy = "#696969",
		AshfallHeavyForeground = "#FFFFFF",
		SmallVolcanicBombFall = "#ff6666",
		SmallVolcanicBombFallForeground = "#000000",

		WeatherWarningLevel5Color = "#0C000C",
		WeatherWarningLevel4Color = "#AA00AA",
		WeatherWarningLevel3Color = "#FF2800",
		WeatherWarningLevel2Color = "#F2E700",

		MarineWarningTyphoonColor = "#80800080",
		MarineWarningStormColor = "#80DC143C",
		MarineWarningGaleColor = "#80FF8C00",
		MarineWarningWindColor = "#80FFD700",
	};

	public static WindowTheme[] DefaultThemes { get; } = [
		Dark,
		Light,
		Quarog,
	];
}
