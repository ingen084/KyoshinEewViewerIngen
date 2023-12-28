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
	/// <summary>
	/// 地方海上予報区
	/// </summary>
	LocalMarineForecastArea,
}
