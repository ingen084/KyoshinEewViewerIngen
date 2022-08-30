using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

public struct WarningAreaPart
{
	private XmlNode Node { get; set; }

	public WarningAreaPart(XmlNode node)
	{
		Node = node;
	}

	private string? type = null;
	/// <summary>
	/// 風域の種類<br/>
	/// <list type="bullet">
	/// <item>強風域</item>
	/// <item>暴風域</item>
	/// <item>暴風警戒域</item>
	/// </list>
	/// </summary>
	public string Type => type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません"));

	/// <summary>
	/// 暴風域･強風域の風速
	/// </summary>
	public IEnumerable<PhysicalQuantity> WindSpeeds
		=> Node.Children.Where(c => c.Name == Literals.JmaEbWindSpeed()).Select(c => new PhysicalQuantity(c));

	private TyphoonCircle? circle = null;
	/// <summary>
	/// 暴風域･強風域の風域情報
	/// </summary>
	public TyphoonCircle Circle => circle ??= (Node.TryFindChild(Literals.JmaEbCircle(), out var n) ? new(n) : throw new JmaXmlParseException("Circle ノードが存在しません"));
}
