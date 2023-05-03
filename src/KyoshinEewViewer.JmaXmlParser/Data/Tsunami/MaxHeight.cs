using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct MaxHeight
{
	private XmlNode Node { get; set; }

	public MaxHeight(XmlNode node)
	{
		Node = node;
	}

	private PhysicalQuantity? _tsunamiHeight = null;
	/// <summary>
	/// 予想される津波の高さ<br/>
	/// マグニチュードが 8 を超える巨大地震と推定されるなど、地震規模推定の不確定性が大きい場合は condition=不明 高さは NaN になる
	/// また、 description に定性的表現(巨大/高い) 津波予報･注意報の場合は空
	/// </summary>
	public PhysicalQuantity TsunamiHeight => _tsunamiHeight ??= (Node.TryFindChild("jmx_eb:TsunamiHeight"u8, out var c) ? new PhysicalQuantity(c) : throw new JmaXmlParseException("Kind ノードが存在しません"));

	private string? _revise = null;
	/// <summary>
	/// 新たに出現した場合は 追加<br/>
	/// 既出かつ更新された場合は 更新
	/// </summary>
	public string? Revise => _revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);
}
