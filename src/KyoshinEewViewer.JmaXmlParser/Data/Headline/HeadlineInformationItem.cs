using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

public struct HeadlineInformationItem
{
	private XmlNode Node { get; set; }

	public HeadlineInformationItem(XmlNode node)
	{
		Node = node;
	}

	private Kind? kind = null;
	/// <summary>
	/// 事項種別(先頭1件のみ)
	/// </summary>
	public Kind Kind => kind ??= (Node.TryFindChild(Literals.Kind(), out var c) ? new(c) : throw new JmaXmlParseException("Kind ノードが存在しません"));

	/// <summary>
	/// 事項種別(全件)
	/// </summary>
	public IEnumerable<Kind> Kinds
		=> Node.Children.Where(c => c.Name == Literals.Kind()).Select(c => new Kind(c));

	private Kind? lastKind = null;
	/// <summary>
	/// 事項種別(変化前/先頭1件のみ)<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public Kind? LastKind => lastKind ??= (Node.TryFindChild(Literals.LastKind(), out var c) ? new(c) : null);

	/// <summary>
	/// 事項種別(変化前/全件)
	/// </summary>
	public IEnumerable<Kind> LastKinds
		=> Node.Children.Where(c => c.Name == Literals.LastKind()).Select(c => new Kind(c));

	private string? areaCodeType = null;
	/// <summary>
	/// エリアコードの種類 存在しない場合もある
	/// </summary>
	public string? AreaCodeType {
		get {
			if (areaCodeType != null)
				return areaCodeType;
			if (!Node.TryFindChild(Literals.Areas(), out var n))
				return null;
			if (!n.TryFindAttribute(Literals.AttrCodeType(), out var c))
				throw new JmaXmlParseException("codeType 属性が存在しません");
			return areaCodeType = c.Value.ToString();
		}
	}

	/// <summary>
	/// 対象地域 存在しない場合もある
	/// </summary>
	public IEnumerable<HeadlineInformationArea> Areas
	{
		get {
			if (!Node.TryFindChild(Literals.Areas(), out var n))
				return Enumerable.Empty<HeadlineInformationArea>();
			return n.Children.Where(c => c.Name == Literals.Area()).Select(c => new HeadlineInformationArea(c));
		}
	}
}
