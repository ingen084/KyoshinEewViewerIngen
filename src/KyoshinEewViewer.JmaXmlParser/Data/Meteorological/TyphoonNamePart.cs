using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

public struct TyphoonNamePart
{
	private XmlNode Node { get; set; }

	public TyphoonNamePart(XmlNode node)
	{
		Node = node;
	}

	private string? _name = null;
	/// <summary>
	/// 台風の英字の呼名
	/// </summary>
	public string? Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : null);

	private string? _nameKana = null;
	/// <summary>
	/// 台風のカナの呼名
	/// </summary>
	public string? NameKana => _nameKana ??= (Node.TryFindStringNode(Literals.NameKana(), out var n) ? n : null);

	private string? _number = null;
	/// <summary>
	/// 台風番号
	/// </summary>
	public string? Number => _number ??= (Node.TryFindStringNode(Literals.Number(), out var n) ? n : null);

	private string? _remark = null;
	/// <summary>
	/// 台風番号
	/// <list type="bullet">
	/// <item>台風発生</item>
	/// <item>台風発生（域外から入る）</item>
	/// <item>台風消滅（域外へ出る）</item>
	/// <item>台風消滅（温帯低気圧化）</item>
	/// <item>台風消滅（熱帯低気圧化）</item>
	/// <item>台風発生の可能性が小さくなった</item>
	/// <item>発表間隔変更（毎時から３時間毎）</item>
	/// <item>発表間隔変更（３時間毎から毎時）</item>
	/// <item>台風発生予想</item>
	/// <item>温帯低気圧化しつつある</item>
	/// </list>
	/// </summary>
	public string? Remark => _remark ??= (Node.TryFindStringNode(Literals.Remark(), out var n) ? n : null);
}
