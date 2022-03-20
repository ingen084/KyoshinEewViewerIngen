using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

public class HeadlineInformationItem
{
	private XmlNode Node { get; set; }

	public HeadlineInformationItem(XmlNode node)
	{
		Node = node;
	}

	private HeadlineInformationKind? kind;
	/// <summary>
	/// 事項種別(先頭1件のみ)
	/// </summary>
	public HeadlineInformationKind Kind => kind ??= (Node.TryFindChild(Literals.Kind(), out var c) ? new(c) : throw new JmaXmlParseException("Kind ノードが存在しません"));

	/// <summary>
	/// 事項種別(全件)
	/// </summary>
	public IEnumerable<HeadlineInformationKind> Kinds
	{
		get {
			foreach (var info in Node.Children.Where(c => c.Name == Literals.Kind()))
				yield return new(info);
		}
	}

	private HeadlineInformationKind? lastKind;
	/// <summary>
	/// 事項種別(変化前/先頭1件のみ)<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	public HeadlineInformationKind? LastKind => lastKind ??= (Node.TryFindChild(Literals.LastKind(), out var c) ? new(c) : null);

	/// <summary>
	/// 事項種別(変化前/全件)
	/// </summary>
	public IEnumerable<HeadlineInformationKind> LastKinds
	{
		get {
			foreach (var info in Node.Children.Where(c => c.Name == Literals.LastKind()))
				yield return new(info);
		}
	}

	private string? areaCodeType;
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
				yield break;
			foreach (var info in n.Children.Where(c => c.Name == Literals.Area()))
				yield return new(info);
		}
	}
}
