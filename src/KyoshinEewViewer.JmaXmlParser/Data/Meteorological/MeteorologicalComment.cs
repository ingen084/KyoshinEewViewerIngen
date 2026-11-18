using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 気象情報に付加する解説。
/// </summary>
public struct MeteorologicalComment(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 種類ごとの解説文。
	/// </summary>
	public IEnumerable<MeteorologicalText> Texts
		=> Node.Children.Where(c => c.Name == "Text"u8).Select(c => new MeteorologicalText(c));

	/// <summary>
	/// 解説文のコード（空白区切りの原文）。存在しない場合は null。
	/// </summary>
	public string? Code => Node.TryFindStringNode("Code"u8, out var n) ? n : null;
}
