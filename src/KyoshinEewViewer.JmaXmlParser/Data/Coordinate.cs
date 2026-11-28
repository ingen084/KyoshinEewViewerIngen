namespace KyoshinEewViewer.JmaXmlParser.Data;

[JmaXmlType]
public sealed partial class Coordinate<TView>
{
	/// <summary>
	/// 文字列表現<br/>
	/// 例: 北緯３９．０度 東経１４０．９度 深さ １０ｋｍ、震源要素不明
	/// </summary>
	[JmaXml("@description!")]
	public partial string Description { get; }

	/// <summary>
	/// 座標系 そもそも座標が不明の場合は null<br/>
	/// 例: 日本測地系
	/// </summary>
	[JmaXml("@datum?")]
	public partial string? Datum { get; }

	/// <summary>
	/// 世界測地系 の場合 震源位置（度分） が入る
	/// </summary>
	[JmaXml("@type?")]
	public partial string? Type { get; }

	/// <summary>
	/// ISO6709 に準拠した座標表記<br/>
	/// 座標が不明な場合空になる<br/>
	/// 例: +39.0+140.9-10000/
	/// </summary>
	[JmaXml(".")]
	public partial string? Value { get; }
}
