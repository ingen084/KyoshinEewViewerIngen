using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct MeteorologicalInfoKindProperty
{
	private XmlNode Node { get; set; }

	public MeteorologicalInfoKindProperty(XmlNode node)
	{
		Node = node;
	}

	private string? type = null;
	/// <summary>
	/// 要素名
	/// </summary>
	public string Type => type ??= (Node.TryFindStringNode(Literals.Type(), out var n) ? n : throw new JmaXmlParseException("Type ノードが存在しません"));

	private TyphoonNamePart? typhoonNamePart = null;
	/// <summary>
	/// Type=呼称 のときに出現
	/// </summary>
	public TyphoonNamePart TyphoonNamePart => typhoonNamePart ??= (Node.TryFindChild(Literals.TyphoonNamePart(), out var n) ? new(n) : throw new JmaXmlParseException("TyphoonNamePart ノードが存在しません"));

	private ClassPart? classPart = null;
	/// <summary>
	/// Type=階級 のときに出現
	/// </summary>
	public ClassPart ClassPart => classPart ??= (Node.TryFindChild(Literals.ClassPart(), out var n) ? new(n) : throw new JmaXmlParseException("ClassPart ノードが存在しません"));

	private CenterPart? centerPart = null;
	/// <summary>
	/// Type=中心 のときに出現
	/// </summary>
	public CenterPart CenterPart => centerPart ??= (Node.TryFindChild(Literals.CenterPart(), out var n) ? new(n) : throw new JmaXmlParseException("CenterPart ノードが存在しません"));

	private WindPart? windPart = null;
	/// <summary>
	/// Type=風 のときに出現
	/// </summary>
	public WindPart WindPart => windPart ??= (Node.TryFindChild(Literals.WindPart(), out var n) ? new(n) : throw new JmaXmlParseException("WindPart ノードが存在しません"));

	/// <summary>
	/// Type=風 のときに出現
	/// </summary>
	public IEnumerable<WarningAreaPart> WarningAreaParts
		=> Node.Children.Where(c => c.Name == Literals.WarningAreaPart()).Select(c => new WarningAreaPart(c));
}
