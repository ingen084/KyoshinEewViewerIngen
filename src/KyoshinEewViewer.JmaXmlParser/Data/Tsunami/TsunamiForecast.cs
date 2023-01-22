using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiForecast
{
	private XmlNode Node { get; set; }

	public TsunamiForecast(XmlNode node)
	{
		Node = node;
	}

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
	/// コード体系の定義
	/// </summary>
	public IEnumerable<TsunamiForecastItem> Items
		=> Node.Children.Where(c => c.Name == Literals.Item()).Select(c => new TsunamiForecastItem(c));
}
