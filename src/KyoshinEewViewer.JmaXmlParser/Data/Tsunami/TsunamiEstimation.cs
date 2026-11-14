using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

/// <summary>
/// 沖合の観測から推定した沿岸地域の津波情報。
/// </summary>
public struct TsunamiEstimation(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// コード体系の定義。
	/// </summary>
	public IEnumerable<CodeDefineType> CodeDefineTypes => Node.TryFindChild(Literals.CodeDefine(), out var n)
		? n.Children.Where(c => c.Name == Literals.Type()).Select(c => new CodeDefineType(c))
		: Enumerable.Empty<CodeDefineType>();

	/// <summary>
	/// 沿岸地域ごとの推定値。
	/// </summary>
	public IEnumerable<TsunamiEstimationItem> Items
		=> Node.Children.Where(c => c.Name == Literals.Item()).Select(c => new TsunamiEstimationItem(c));
}
