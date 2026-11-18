using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 予測震度・予測長周期地震動階級の変化に関する付加要素。
/// </summary>
public struct IntensityAppendix(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 最大予測震度の変化。
	/// </summary>
	public int MaxIntChange => Node.TryFindIntNode(Literals.MaxIntChange(), out var n) ? n : throw new JmaXmlParseException("MaxIntChange ノードが存在しません");

	/// <summary>
	/// 最大予測長周期地震動階級の変化。存在しない場合は null。
	/// </summary>
	public int? MaxLgIntChange => Node.TryFindIntNode(Literals.MaxLgIntChange(), out var n) ? n : null;

	/// <summary>
	/// 最大予測震度が変化した理由。
	/// </summary>
	public int MaxIntChangeReason => Node.TryFindIntNode(Literals.MaxIntChangeReason(), out var n) ? n : throw new JmaXmlParseException("MaxIntChangeReason ノードが存在しません");

	/// <summary>
	/// 最大予測長周期地震動階級が変化した理由。存在しない場合は null。
	/// </summary>
	public int? MaxLgIntChangeReason => Node.TryFindIntNode("MaxLgIntChangeReason"u8, out var n) ? n : null;
}
