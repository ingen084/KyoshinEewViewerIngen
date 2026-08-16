using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace KyoshinEewViewer.Core.Models;

public class WindowTheme : ObservableObject
{
	private string _name = "";
	public required string Name
	{
		get => _name;
		set => SetProperty(ref _name, value);
	}

	private string _titleBackgroundColor = "";
	/// <summary>
	/// ウィンドウのタイトルバーの背景色
	/// </summary>
	public string TitleBackgroundColor
	{
		get => _titleBackgroundColor;
		set => SetProperty(ref _titleBackgroundColor, value);
	}

	private bool _isDark;
	/// <summary>
	/// ボタンなどのスタイルをダークテーマ調にするか
	/// </summary>
	public bool IsDark
	{
		get => _isDark;
		set => SetProperty(ref _isDark, value);
	}

	private string _overseasLandColor = "";
	/// <summary>
	/// 地図配色 海外地形(ボーダーは設定不可)
	/// </summary>
	public string OverseasLandColor
	{
		get => _overseasLandColor;
		set => SetProperty(ref _overseasLandColor, value);
	}

	private string _landColor = "";
	/// <summary>
	/// 地図配色 地形
	/// </summary>
	public string LandColor
	{
		get => _landColor;
		set => SetProperty(ref _landColor, value);
	}

	private string _landStrokeColor = "";
	/// <summary>
	/// 地図配色 海岸線
	/// </summary>
	public string LandStrokeColor
	{
		get => _landStrokeColor;
		set => SetProperty(ref _landStrokeColor, value);
	}

	private double _landStrokeThickness = 0.6;
	/// <summary>
	/// 地図配色 海岸線の太さ
	/// 0 にすることで軽量化できる
	/// </summary>
	public double LandStrokeThickness
	{
		get => _landStrokeThickness;
		set => SetProperty(ref _landStrokeThickness, value);
	}

	private string _prefStrokeColor = "";
	/// <summary>
	/// 地図配色 都道府県境界線
	/// </summary>
	public string PrefStrokeColor
	{
		get => _prefStrokeColor;
		set => SetProperty(ref _prefStrokeColor, value);
	}

	private double _prefStrokeThickness = 0.6;
	/// <summary>
	/// 地図配色 都道府県境界線の太さ
	/// </summary>
	public double PrefStrokeThickness
	{
		get => _prefStrokeThickness;
		set => SetProperty(ref _prefStrokeThickness, value);
	}

	private string _areaStrokeColor = "";
	/// <summary>
	/// 地図配色 地域境界線
	/// </summary>
	public string AreaStrokeColor
	{
		get => _areaStrokeColor;
		set => SetProperty(ref _areaStrokeColor, value);
	}

	private double _areaStrokeThickness = 0.4;
	/// <summary>
	/// 地図配色 地域境界線の太さ
	/// </summary>
	public double AreaStrokeThickness
	{
		get => _areaStrokeThickness;
		set => SetProperty(ref _areaStrokeThickness, value);
	}

	private string _mainBackgroundColor = "";
	/// <summary>
	/// メイン背景色
	/// </summary>
	public string MainBackgroundColor
	{
		get => _mainBackgroundColor;
		set => SetProperty(ref _mainBackgroundColor, value);
	}

	private string _foregroundColor = "";
	/// <summary>
	/// メイン文字色
	/// </summary>
	public string ForegroundColor
	{
		get => _foregroundColor;
		set => SetProperty(ref _foregroundColor, value);
	}

	private string _subForegroundColor = "";
	/// <summary>
	/// サブ文字色(補足等)
	/// </summary>
	public string SubForegroundColor
	{
		get => _subForegroundColor;
		set => SetProperty(ref _subForegroundColor, value);
	}

	private string _emphasisForegroundColor = "";
	/// <summary>
	/// 強調文字(現状では強震モニタリプレイ時の時刻色)
	/// </summary>
	public string EmphasisForegroundColor
	{
		get => _emphasisForegroundColor;
		set => SetProperty(ref _emphasisForegroundColor, value);
	}


	private string _dockBackgroundColor = "";
	/// <summary>
	/// ドック(要素ウィンドウ)背景色
	/// </summary>
	public string DockBackgroundColor
	{
		get => _dockBackgroundColor;
		set => SetProperty(ref _dockBackgroundColor, value);
	}

	private string _dockTitleBackgroundColor = "";
	/// <summary>
	/// ドック(要素ウィンドウ)タイトル部分背景色
	/// </summary>
	public string DockTitleBackgroundColor
	{
		get => _dockTitleBackgroundColor;
		set => SetProperty(ref _dockTitleBackgroundColor, value);
	}

	private string _dockWarningBackgroundColor = "";
	/// <summary>
	/// ドックエラー･警告配色背景色
	/// </summary>
	public string DockWarningBackgroundColor
	{
		get => _dockWarningBackgroundColor;
		set => SetProperty(ref _dockWarningBackgroundColor, value);
	}

	private string _dockWarningTitleBackgroundColor = "";
	/// <summary>
	/// ドックエラー･警告配色タイトル部分背景色
	/// </summary>
	public string DockWarningTitleBackgroundColor
	{
		get => _dockWarningTitleBackgroundColor;
		set => SetProperty(ref _dockWarningTitleBackgroundColor, value);
	}

	private string _warningForegroundColor = "";
	/// <summary>
	/// エラー･警告文字色
	/// </summary>
	public string WarningForegroundColor
	{
		get => _warningForegroundColor;
		set => SetProperty(ref _warningForegroundColor, value);
	}

	private string _warningSubForegroundColor = "";
	/// <summary>
	/// エラー･警告サブ文字色
	/// </summary>
	public string WarningSubForegroundColor
	{
		get => _warningSubForegroundColor;
		set => SetProperty(ref _warningSubForegroundColor, value);
	}

	private string _warningBackgroundColor = "";
	/// <summary>
	/// エラー･警告背景色
	/// </summary>
	public string WarningBackgroundColor
	{
		get => _warningBackgroundColor;
		set => SetProperty(ref _warningBackgroundColor, value);
	}

	private string _tsunamiForecastColor = "";
	/// <summary>
	/// 津波予報色
	/// </summary>
	public string TsunamiForecastColor
	{
		get => _tsunamiForecastColor;
		set => SetProperty(ref _tsunamiForecastColor, value);
	}

	private string _tsunamiForecastForegroundColor = "";
	/// <summary>
	/// 津波予報文字色
	/// </summary>
	public string TsunamiForecastForegroundColor
	{
		get => _tsunamiForecastForegroundColor;
		set => SetProperty(ref _tsunamiForecastForegroundColor, value);
	}

	private string _tsunamiAdvisoryColor = "";
	/// <summary>
	/// 津波注意報色
	/// </summary>
	public string TsunamiAdvisoryColor
	{
		get => _tsunamiAdvisoryColor;
		set => SetProperty(ref _tsunamiAdvisoryColor, value);
	}

	private string _tsunamiAdvisoryForegroundColor = "";
	/// <summary>
	/// 津波注意報文字色
	/// </summary>
	public string TsunamiAdvisoryForegroundColor
	{
		get => _tsunamiAdvisoryForegroundColor;
		set => SetProperty(ref _tsunamiAdvisoryForegroundColor, value);
	}

	private string _tsunamiWarningColor = "";
	/// <summary>
	/// 津波警報色
	/// </summary>
	public string TsunamiWarningColor
	{
		get => _tsunamiWarningColor;
		set => SetProperty(ref _tsunamiWarningColor, value);
	}

	private string _tsunamiWarningForegroundColor = "";
	/// <summary>
	/// 津波警報文字色
	/// </summary>
	public string TsunamiWarningForegroundColor
	{
		get => _tsunamiWarningForegroundColor;
		set => SetProperty(ref _tsunamiWarningForegroundColor, value);
	}

	private string _tsunamiMajorWarningColor = "";
	/// <summary>
	/// 大津波警報色
	/// </summary>
	public string TsunamiMajorWarningColor
	{
		get => _tsunamiMajorWarningColor;
		set => SetProperty(ref _tsunamiMajorWarningColor, value);
	}

	private string _tsunamiMajorWarningForegroundColor = "";
	/// <summary>
	/// 大津波警報文字色
	/// </summary>
	public string TsunamiMajorWarningForegroundColor
	{
		get => _tsunamiMajorWarningForegroundColor;
		set => SetProperty(ref _tsunamiMajorWarningForegroundColor, value);
	}

	private string _earthquakeHypocenterBorderColor = "";
	/// <summary>
	/// 震央アイコンボーダー色(地震情報)
	/// </summary>
	public string EarthquakeHypocenterBorderColor
	{
		get => _earthquakeHypocenterBorderColor;
		set => SetProperty(ref _earthquakeHypocenterBorderColor, value);
	}

	private string _earthquakeHypocenterColor = "";
	/// <summary>
	/// 震央アイコン中央色(地震情報)
	/// </summary>
	public string EarthquakeHypocenterColor
	{
		get => _earthquakeHypocenterColor;
		set => SetProperty(ref _earthquakeHypocenterColor, value);
	}

	private string _eewForecastHypocenterBorderColor = "";
	/// <summary>
	/// 震央アイコンボーダー色(緊急地震速報 予報)
	/// </summary>
	public string EewForecastHypocenterBorderColor
	{
		get => _eewForecastHypocenterBorderColor;
		set => SetProperty(ref _eewForecastHypocenterBorderColor, value);
	}

	private string _eewForecastHypocenterColor = "";
	/// <summary>
	/// 震央アイコン中央色(緊急地震速報 予報)
	/// </summary>
	public string EewForecastHypocenterColor
	{
		get => _eewForecastHypocenterColor;
		set => SetProperty(ref _eewForecastHypocenterColor, value);
	}

	private string _eewWarningHypocenterBorderColor = "";
	/// <summary>
	/// 震央アイコンボーダー色(緊急地震速報 警報)
	/// </summary>
	public string EewWarningHypocenterBorderColor
	{
		get => _eewWarningHypocenterBorderColor;
		set => SetProperty(ref _eewWarningHypocenterBorderColor, value);
	}

	private string _eewWarningHypocenterColor = "";
	/// <summary>
	/// 震央アイコン中央色(緊急地震速報 警報)
	/// </summary>
	public string EewWarningHypocenterColor
	{
		get => _eewWarningHypocenterColor;
		set => SetProperty(ref _eewWarningHypocenterColor, value);
	}

	private bool _isEewHypocenterBlinkAnimation = true;
	/// <summary>
	/// 緊急地震速報震央アイコンの点滅アニメーションを有効にするか
	/// </summary>
	public bool IsEewHypocenterBlinkAnimation
	{
		get => _isEewHypocenterBlinkAnimation;
		set => SetProperty(ref _isEewHypocenterBlinkAnimation, value);
	}

	private string _eewForecastPWaveColor = "";
	/// <summary>
	/// 緊急地震速報(予報)P波色
	/// </summary>
	public string EewForecastPWaveColor
	{
		get => _eewForecastPWaveColor;
		set => SetProperty(ref _eewForecastPWaveColor, value);
	}

	private string _eewForecastSWaveColor = "";
	/// <summary>
	/// 緊急地震速報(予報)S波色
	/// </summary>
	public string EewForecastSWaveColor
	{
		get => _eewForecastSWaveColor;
		set => SetProperty(ref _eewForecastSWaveColor, value);
	}

	private bool _isEewForecastSWaveGradient = true;
	/// <summary>
	/// 緊急地震速報(予報)のS波色をグラデーションにするか
	/// </summary>
	public bool IsEewForecastSWaveGradient
	{
		get => _isEewForecastSWaveGradient;
		set => SetProperty(ref _isEewForecastSWaveGradient, value);
	}

	private string _eewWarningPWaveColor = "";
	/// <summary>
	/// 緊急地震速報(警報)P波色
	/// </summary>
	public string EewWarningPWaveColor
	{
		get => _eewWarningPWaveColor;
		set => SetProperty(ref _eewWarningPWaveColor, value);
	}

	private string _eewWarningSWaveColor = "";
	/// <summary>
	/// 緊急地震速報(警報)S波色
	/// </summary>
	public string EewWarningSWaveColor
	{
		get => _eewWarningSWaveColor;
		set => SetProperty(ref _eewWarningSWaveColor, value);
	}

	private bool _isEewWarningSWaveGradient = true;
	/// <summary>
	/// 緊急地震速報(警報)のS波色をグラデーションにするか
	/// </summary>
	public bool IsEewWarningSWaveGradient
	{
		get => _isEewWarningSWaveGradient;
		set => SetProperty(ref _isEewWarningSWaveGradient, value);
	}

	private string _ashfallLight = "";
	/// <summary>
	/// 降灰予報における『少量の降灰』
	/// </summary>
	public string AshfallLight
	{
		get => _ashfallLight;
		set => SetProperty(ref _ashfallLight, value);
	}

	private string _ashfallLightForeground = "";
	/// <summary>
	/// 降灰予報における『少量の降灰』文字色
	/// </summary>
	public string AshfallLightForeground
	{
		get => _ashfallLightForeground;
		set => SetProperty(ref _ashfallLightForeground, value);
	}

	private string _ashfallModerate = "";
	/// <summary>
	/// 降灰予報における『やや多量の降灰』
	/// </summary>
	public string AshfallModerate
	{
		get => _ashfallModerate;
		set => SetProperty(ref _ashfallModerate, value);
	}

	private string _ashfallModerateForeground = "";
	/// <summary>
	/// 降灰予報における『やや多量の降灰』文字色
	/// </summary>
	public string AshfallModerateForeground
	{
		get => _ashfallModerateForeground;
		set => SetProperty(ref _ashfallModerateForeground, value);
	}

	private string _ashfallHeavy = "";
	/// <summary>
	/// 降灰予報における『大量の降灰』
	/// </summary>
	public string AshfallHeavy
	{
		get => _ashfallHeavy;
		set => SetProperty(ref _ashfallHeavy, value);
	}

	private string _ashfallHeavyForeground = "";
	/// <summary>
	/// 降灰予報における『大量の降灰』文字色
	/// </summary>
	public string AshfallHeavyForeground
	{
		get => _ashfallHeavyForeground;
		set => SetProperty(ref _ashfallHeavyForeground, value);
	}

	private string _smallVolcanicBombFall = "";
	/// <summary>
	/// 降灰予報における『小さな噴石の落下』
	/// </summary>
	public string SmallVolcanicBombFall
	{
		get => _smallVolcanicBombFall;
		set => SetProperty(ref _smallVolcanicBombFall, value);
	}

	private string _smallVolcanicBombFallForeground = "";
	/// <summary>
	/// 降灰予報における『小さな噴石の落下』文字色
	/// </summary>
	public string SmallVolcanicBombFallForeground
	{
		get => _smallVolcanicBombFallForeground;
		set => SetProperty(ref _smallVolcanicBombFallForeground, value);
	}

	private string _weatherWarningLevel5Color = "";
	/// <summary>
	/// 気象 - 警戒レベル5（特別警報）
	/// </summary>
	public string WeatherWarningLevel5Color
	{
		get => _weatherWarningLevel5Color;
		set => SetProperty(ref _weatherWarningLevel5Color, value);
	}

	private string _weatherWarningLevel4Color = "";
	/// <summary>
	/// 気象 - 警戒レベル4（危険警報･土砂災害警戒情報）
	/// </summary>
	public string WeatherWarningLevel4Color
	{
		get => _weatherWarningLevel4Color;
		set => SetProperty(ref _weatherWarningLevel4Color, value);
	}

	private string _weatherWarningLevel3Color = "";
	/// <summary>
	/// 気象 - 警戒レベル3（警報）
	/// </summary>
	public string WeatherWarningLevel3Color
	{
		get => _weatherWarningLevel3Color;
		set => SetProperty(ref _weatherWarningLevel3Color, value);
	}

	private string _weatherWarningLevel2Color = "";
	/// <summary>
	/// 気象 - 警戒レベル2（注意報）
	/// </summary>
	public string WeatherWarningLevel2Color
	{
		get => _weatherWarningLevel2Color;
		set => SetProperty(ref _weatherWarningLevel2Color, value);
	}

	private string _marineWarningTyphoonColor = "";
	/// <summary>
	/// 海上警報 - 海上台風警報
	/// </summary>
	public string MarineWarningTyphoonColor
	{
		get => _marineWarningTyphoonColor;
		set => SetProperty(ref _marineWarningTyphoonColor, value);
	}

	private string _marineWarningStormColor = "";
	/// <summary>
	/// 海上警報 - 海上暴風警報
	/// </summary>
	public string MarineWarningStormColor
	{
		get => _marineWarningStormColor;
		set => SetProperty(ref _marineWarningStormColor, value);
	}

	private string _marineWarningGaleColor = "";
	/// <summary>
	/// 海上警報 - 海上強風警報
	/// </summary>
	public string MarineWarningGaleColor
	{
		get => _marineWarningGaleColor;
		set => SetProperty(ref _marineWarningGaleColor, value);
	}

	private string _marineWarningWindColor = "";
	/// <summary>
	/// 海上警報 - 海上風警報
	/// </summary>
	public string MarineWarningWindColor
	{
		get => _marineWarningWindColor;
		set => SetProperty(ref _marineWarningWindColor, value);
	}

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
