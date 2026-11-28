using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;
public struct Area(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 対象の市町村。存在しない場合は空。
	/// </summary>
	public IEnumerable<TsunamiCity> Cities
		=> Node.Children.Where(c => c.Name == Literals.City()).Select(c => new TsunamiCity(c));

	private string? _name = null;
	/// <summary>
	/// 地域名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private int? _code = null;
	/// <summary>
	/// 地域コード
	/// </summary>
	public int Code => _code ??= (Node.TryFindIntNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));
}
