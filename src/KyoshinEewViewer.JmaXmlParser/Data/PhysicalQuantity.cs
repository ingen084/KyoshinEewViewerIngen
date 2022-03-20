using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;

/// <summary>
/// 物理量を表す<br/>
/// 例: 気圧、気温、風向、風速、湿度、震度、マグニチュード など
/// </summary>
public class PhysicalQuantity
{
	private XmlNode Node { get; set; }

	public PhysicalQuantity(XmlNode node)
	{
		Node = node;
	}

	private string? name;
	/// <summary>
	/// 地震のノードの名前<br/>
	/// 例: Pressure、Temperature
	/// </summary>
	public string Name => name ??= Node.Name.ToString();

	private string? type;
	/// <summary>
	/// 基本要素の種別<br/>
	/// 例: 気圧、最低気温、風向、風速、Mj
	/// </summary>
	public string Type => type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません"));

	private string? unit;
	/// <summary>
	/// 単位<br/>
	/// 例: hPa、度、１６方位漢字、m/s、ノット<br/>
	/// マグニチュードの場合 <c>null</c>
	/// </summary>
	public string? Unit => unit ??= (Node.TryFindStringAttribute(Literals.AttrUnit(), out var n) ? n : null);

	private int? refId;
	/// <summary>
	/// 時系列の際の参照番号<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public int? RefId => refId ??= (Node.TryFindIntAttribute(Literals.AttrRefId(), out var n) ? n : null);

	private string? condition;
	/// <summary>
	/// 値の状態など<br/>
	/// 例: 約、以上、不明<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public string? Condition => condition ??= (Node.TryFindStringAttribute(Literals.AttrCondition(), out var n) ? n : null);

	private string? description;
	/// <summary>
	/// 文字列表現<br/>
	/// 例: 気圧不明、風向不明、風速不明、M6.6、M 不明<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public string? Description => description ??= (Node.TryFindStringAttribute(Literals.AttrDescription(), out var n) ? n : null);

	private string? value;
	/// <summary>
	/// 値
	/// </summary>
	public string Value => value ??= Node.InnerText.ToString();

	public bool TryGetIntValue(out int value)
		=> Node.InnerText.TryToInt32(out value);

	public bool TryGetFloatValue(out float value)
		=> Node.InnerText.TryToFloat32(out value);
}
