using System;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct MeteorologicalInfo
{
	private XmlNode Node { get; set; }

	public MeteorologicalInfo(XmlNode node)
	{
		Node = node;
	}

	private DateTimeOffset? dateTime = null;
	/// <summary>
	/// 予報や観測時刻
	/// </summary>
	public DateTimeOffset DateTime => dateTime ??= (Node.TryFindDateTimeNode(Literals.DateTime(), out var n) ? n : throw new JmaXmlParseException("DateTime ノードが存在しません"));

	private string? dateTimeType = null;
	/// <summary>
	/// 予報や観測時刻の種類
	/// </summary>
	public string? DateTimeType
	{
		get {
			if (dateTimeType != null)
				return dateTimeType;
			if (!Node.TryFindChild(Literals.DateTime(), out var n))
				throw new JmaXmlParseException("DateTime ノードが存在しません");
			if (!n.TryFindStringAttribute(Literals.AttrType(), out var r))
				throw new JmaXmlParseException("type 属性が存在しません");
			return dateTimeType = r;
		}
	}

	/// <summary>
	/// 各気象情報の予報や観測事項
	/// </summary>
	public IEnumerable<MeteorologicalInfoKindProperty> MeteorologicalInfoKindProperties
	{
		get {
			if (!Node.TryFindChild(Literals.Item(), out var k))
				return Enumerable.Empty<MeteorologicalInfoKindProperty>();
			return k.Children.Where(c => c.Name == Literals.Kind())
				.Select(c => c.TryFindChild(Literals.Property(), out var p) ? new MeteorologicalInfoKindProperty(p) : throw new JmaXmlParseException("Property ノードが存在しません"));
		}
	}

	/// <summary>
	/// 熱帯低気圧の強風域･暴風域の情報
	/// </summary>
	public IEnumerable<TyphoonCircle> TyphoonCircles
	{
		get {
			if (!Node.TryFindChild(Literals.Item(), out var k))
				throw new JmaXmlParseException("Item ノードが存在しません");
			if (!k.TryFindChild(Literals.Area(), out var n))
				throw new JmaXmlParseException("Area ノードが存在しません");
			return n.Children.Where(c => c.Name == Literals.JmaEbCircle()).Select(c => new TyphoonCircle(c));
		}
	}
}
