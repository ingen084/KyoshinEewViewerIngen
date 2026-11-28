namespace KyoshinEewViewer.JmaXmlParser.Data;

/// <summary>
/// 物理量を表す<br/>
/// 例: 気圧、気温、風向、風速、湿度、震度、マグニチュード など
/// </summary>
[JmaXmlType]
public sealed partial class PhysicalQuantity<TView>
{
	/// <summary>
	/// 自身のノードの名前<br/>
	/// 例: Pressure、Temperature
	/// </summary>
	[JmaXml(".name")]
	public partial string Name { get; }

	/// <summary>
	/// 基本要素の種別<br/>
	/// 例: 気圧、最低気温、風向、風速、Mj
	/// </summary>
	[JmaXml("@type!")]
	public partial string Type { get; }

	/// <summary>
	/// 単位<br/>
	/// 例: hPa、度、１６方位漢字、m/s、ノット<br/>
	/// マグニチュードの場合 <c>null</c>
	/// </summary>
	[JmaXml("@unit?")]
	public partial string? Unit { get; }

	/// <summary>
	/// 時系列の際の参照番号<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	[JmaXml("@refID?")]
	public partial int? RefId { get; }

	/// <summary>
	/// 値の状態など<br/>
	/// 例: 約、以上、不明<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	[JmaXml("@condition?")]
	public partial string? Condition { get; }

	/// <summary>
	/// 文字列表現<br/>
	/// 例: 気圧不明、風向不明、風速不明、M6.6、M 不明<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	[JmaXml("@description?")]
	public partial string? Description { get; }

	/// <summary>
	/// 値
	/// </summary>
	[JmaXml(".")]
	public partial string Value { get; }

	[JmaXml(".")]
	public partial bool TryGetIntValue(out int value);

	[JmaXml(".")]
	public partial bool TryGetFloatValue(out float value);
}
