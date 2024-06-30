using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiObservation(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// コード体系の定義
	/// </summary>
	public IEnumerable<CodeDefineType> CodeDefineTypes
	{
		get {
			if (!Node.TryFindChild(Literals.CodeDefine(), out var n))
				return Enumerable.Empty<CodeDefineType>();
			return n.Children.Where(c => c.Name == Literals.Type()).Select(c => new CodeDefineType(c));
		}
	}

	/// <summary>
	/// 津波予報のアイテム
	/// </summary>
	public IEnumerable<TsunamiObservationItem> Items
		=> Node.Children.Where(c => c.Name == Literals.Item()).Select(c => new TsunamiObservationItem(c));
}
