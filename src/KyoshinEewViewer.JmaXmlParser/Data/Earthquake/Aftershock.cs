using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 余震の発生確率に関する情報。
/// </summary>
public struct Aftershock(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 期間ごとの余震発生確率。
	/// </summary>
	public IEnumerable<AftershockItem> Items
		=> Node.Children.Where(c => c.Name == Literals.Item()).Select(c => new AftershockItem(c));

	private string? _text = null;
	/// <summary>
	/// 補足情報。存在しない場合は null。
	/// </summary>
	public string? Text => _text ??= (Node.TryFindStringNode(Literals.Text(), out var n) ? n : null);
}
