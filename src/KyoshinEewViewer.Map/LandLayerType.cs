namespace KyoshinEewViewer.Map;

/// <summary>
/// 地形レイヤーの種類
/// </summary>
public enum LandLayerType
{
	/// <summary>
	/// 日本以外の全地域
	/// </summary>
	WorldWithoutJapan,
	/// <summary>
	/// 一次細分区域
	/// </summary>
	PrimarySubdivisionArea,
	/// <summary>
	/// 府県予報区域
	/// </summary>
	PrefectureForecastArea,
	/// <summary>
	/// 緊急地震速報／地方予報区
	/// </summary>
	RegionForecastAreaForEew,
	/// <summary>
	/// 緊急地震速報／府県予報区
	/// </summary>
	PrefectureForecastAreaForEew,
	/// <summary>
	/// 市町村等（気象警報等）
	/// </summary>
	MunicipalityWeatherWarningArea,
	/// <summary>
	/// 市町村等（地震津波関係）
	/// </summary>
	MunicipalityEarthquakeTsunamiArea,
	/// <summary>
	/// 市町村等をまとめた地域等
	/// </summary>
	BundledMunicipalityArea,
	/// <summary>
	/// 全国・地方予報区等
	/// <para>topojson変換時に全国だけになってしまっている</para>
	/// </summary>
	NationalAndRegionForecastArea,
	/// <summary>
	/// 地震情報／細分区域
	/// </summary>
	EarthquakeInformationSubdivisionArea,
	/// <summary>
	/// 地震情報／都道府県等
	/// </summary>
	EarthquakeInformationPrefecture,
	/// <summary>
	/// 津波予報区
	/// </summary>
	TsunamiForecastArea,
}

public static class LandLayerTypeExtensions
{
	/// <summary>
	/// いくつの数字で割ればひとつ上の地域を分類できるかを返す
	/// </summary>
	/// <param name="t"></param>
	/// <returns>分類できない場合(そもそも広い地域として解釈してほしい場合など)は1</returns>
	public static int GetMultiareaGroupNo(this LandLayerType t)
		=> t switch
		{
			LandLayerType.WorldWithoutJapan => 1,
			LandLayerType.PrimarySubdivisionArea => 1000,
			LandLayerType.PrefectureForecastArea => 1000,
			LandLayerType.RegionForecastAreaForEew => 1,
			LandLayerType.PrefectureForecastAreaForEew => 1,
			LandLayerType.MunicipalityWeatherWarningArea => 100000,
			LandLayerType.MunicipalityEarthquakeTsunamiArea => 100000,
			LandLayerType.BundledMunicipalityArea => 10000,
			LandLayerType.NationalAndRegionForecastArea => 1,
			LandLayerType.EarthquakeInformationSubdivisionArea => 10,
			LandLayerType.EarthquakeInformationPrefecture => 1,
			LandLayerType.TsunamiForecastArea => 1,
			_ => 1,
		};
	/// <summary>
	/// マップの種類を日本語にして返す
	/// </summary>
	/// <param name="t"></param>
	/// <returns></returns>
	public static string? ToJapaneseString(this LandLayerType t)
		=> t switch
		{
			LandLayerType.WorldWithoutJapan => "日本以外の全地域",
			LandLayerType.PrimarySubdivisionArea => "一次細分区域等",
			LandLayerType.PrefectureForecastArea => "府県予報区等",
			LandLayerType.RegionForecastAreaForEew => "緊急地震速報／地方予報区",
			LandLayerType.PrefectureForecastAreaForEew => "緊急地震速報／府県予報区",
			LandLayerType.MunicipalityWeatherWarningArea => "市町村等（気象警報等）",
			LandLayerType.MunicipalityEarthquakeTsunamiArea => "市町村等（地震津波関係）",
			LandLayerType.BundledMunicipalityArea => "市町村等をまとめた地域等",
			LandLayerType.NationalAndRegionForecastArea => "全国・地方予報区等",
			LandLayerType.EarthquakeInformationSubdivisionArea => "地震情報／細分区域",
			LandLayerType.EarthquakeInformationPrefecture => "地震情報／都道府県等",
			LandLayerType.TsunamiForecastArea => "津波予報区",
			_ => null,
		};
}
