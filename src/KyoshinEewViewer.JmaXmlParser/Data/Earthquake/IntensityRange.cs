using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 予測震度または予測長周期地震動階級の範囲。
/// </summary>
public struct IntensityRange(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _from = null;
	/// <summary>
	/// 下限。存在しない場合は null。
	/// </summary>
	public string? From => _from ??= (Node.TryFindStringNode(Literals.From(), out var n) ? n : null);

	private string? _to = null;
	/// <summary>
	/// 上限。「程度以上」の場合は over。存在しない場合は null。
	/// </summary>
	public string? To => _to ??= (Node.TryFindStringNode(Literals.To(), out var n) ? n : null);

	private string? _bound = null;
	/// <summary>
	/// 範囲の境界に関する属性。存在しない場合は null。
	/// </summary>
	public string? Bound => _bound ??= (Node.TryFindStringAttribute("bound"u8, out var n) ? n : null);
}
