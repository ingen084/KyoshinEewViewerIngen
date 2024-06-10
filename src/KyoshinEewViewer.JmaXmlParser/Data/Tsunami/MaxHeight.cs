using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct MaxHeight(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private DateTimeOffset? _dateTime = null;
	/// <summary>
	/// 観測時刻
	/// </summary>
	public DateTimeOffset? DateTime => _dateTime ??= (Node.TryFindDateTimeNode(Literals.DateTime(), out var n) ? n : null);

	private PhysicalQuantity? _tsunamiHeight = null;
	/// <summary>
	/// 予想される津波の高さ<br/>
	/// マグニチュードが 8 を超える巨大地震と推定されるなど、地震規模推定の不確定性が大きい場合は condition=不明 高さは NaN になる
	/// また、 description に定性的表現(巨大/高い) 津波予報･注意報の場合は空
	/// </summary>
	public PhysicalQuantity? TsunamiHeight => _tsunamiHeight ??= (Node.TryFindChild("jmx_eb:TsunamiHeight"u8, out var c) ? new(c) : null);
	
	private string? _condition = null;
	/// <summary>
	/// 観測されたこれまでの最大波が非常に小さい場合、TsunamiHeight の代わりに "微弱"<br/>
	/// 津波警報以上の津波予報区において、観測されたこれまでの最大波の高さが予想される高さに比べて十分小さい場合、TsunamiHeight の代わりに"観測中"<br/>
	/// これまでの最大波の高さが大津波警報の基準を超え、追加あるいは更新された場合は、"重要"
	/// </summary>
	public string? Condition => _condition ??= (Node.TryFindStringNode(Literals.Condition(), out var n) ? n : null);

	private string? _revise = null;
	/// <summary>
	/// 新たに出現した場合は 追加<br/>
	/// 既出かつ更新された場合は 更新
	/// </summary>
	public string? Revise => _revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);
}
