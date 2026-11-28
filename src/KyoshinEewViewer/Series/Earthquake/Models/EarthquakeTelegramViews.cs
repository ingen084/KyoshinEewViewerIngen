using KyoshinEewViewer.JmaXmlParser;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 地震情報の電文から、地震情報の一覧の更新に使う項目を読み取る
/// </summary>
[JmaXmlView]
public partial struct EarthquakeFragmentView;

/// <summary>
/// 津波情報の電文から、震源に関する項目を読み取る
/// </summary>
[JmaXmlView]
public partial struct TsunamiEarthquakeView;

/// <summary>
/// 地震情報の電文から、地域・市区町村・観測点ごとの震度を読み取る
/// </summary>
[JmaXmlView]
internal partial struct EarthquakeStationsView;
