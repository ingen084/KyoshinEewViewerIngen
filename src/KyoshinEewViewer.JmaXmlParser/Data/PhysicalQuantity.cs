using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;

/// <summary>
/// 物理量を表す<br/>
/// 例: 気圧、気温、風向、風速、湿度、震度、マグニチュード など
/// </summary>
public struct PhysicalQuantity
{
	private XmlNode Node { get; set; }

	public PhysicalQuantity(XmlNode node)
	{
		Node = node;
	}

	private string? _name = null;
	/// <summary>
	/// 自身のノードの名前<br/>
	/// 例: Pressure、Temperature
	/// </summary>
	public string Name => _name ??= Node.Name.ToString();

	private string? _type = null;
	/// <summary>
	/// 基本要素の種別<br/>
	/// 例: 気圧、最低気温、風向、風速、Mj
	/// </summary>
	public string Type => _type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません"));

	private string? _unit = null;
	/// <summary>
	/// 単位<br/>
	/// 例: hPa、度、１６方位漢字、m/s、ノット<br/>
	/// マグニチュードの場合 <c>null</c>
	/// </summary>
	public string? Unit => _unit ??= (Node.TryFindStringAttribute(Literals.AttrUnit(), out var n) ? n : null);

	private int? _refId = null;
	/// <summary>
	/// 時系列の際の参照番号<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public int? RefId => _refId ??= (Node.TryFindIntAttribute(Literals.AttrRefId(), out var n) ? n : null);

	private string? _condition = null;
	/// <summary>
	/// 値の状態など<br/>
	/// 例: 約、以上、不明<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public string? Condition => _condition ??= (Node.TryFindStringAttribute(Literals.AttrCondition(), out var n) ? n : null);

	private string? _description = null;
	/// <summary>
	/// 文字列表現<br/>
	/// 例: 気圧不明、風向不明、風速不明、M6.6、M 不明<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public string? Description => _description ??= (Node.TryFindStringAttribute(Literals.AttrDescription(), out var n) ? n : null);

	private string? _value = null;
	/// <summary>
	/// 値
	/// </summary>
	public string Value => _value ??= Node.InnerText.ToString();

	public bool TryGetIntValue(out int value)
		=> Node.InnerText.TryToInt32(out value);

	public bool TryGetFloatValue(out float value)
		=> Node.InnerText.TryToFloat32(out value);
}
