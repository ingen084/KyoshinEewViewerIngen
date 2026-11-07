using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiBody(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _naming = null;
	/// <summary>
	/// 命名地震の名称。存在しない場合は null。
	/// </summary>
	public string? Naming => _naming ??= (Node.TryFindStringNode(Literals.Naming(), out var n) ? n : null);

	/// <summary>
	/// 命名地震の英語名。存在しない場合は null。
	/// </summary>
	public string? NamingEnglish => Node.TryFindChild(Literals.Naming(), out var n) &&
		n.TryFindStringAttribute(Literals.AttrEnglish(), out var english) ? english : null;

	private string? _nextAdvisory = null;
	/// <summary>
	/// 次回発表予定。存在しない場合は null。
	/// </summary>
	public string? NextAdvisory => _nextAdvisory ??= (Node.TryFindStringNode(Literals.NextAdvisory(), out var n) ? n : null);

	private TsunamiData? _tsunami = null;
	/// <summary>
	/// 津波に関連する情報<br/>
	/// InfoType が "取消" の場合、 null
	/// </summary>
	public TsunamiData? Tsunami => _tsunami ??= (Node.TryFindChild("Tsunami"u8, out var n) ? new(n) : null);

	/// <summary>
	/// 地震の諸要素<br/>
	/// InfoType が "取消" の場合、 null<br/>
	/// 複数の地震が原因の場合は地震ごとに項目が存在する
	/// </summary>
	public IEnumerable<EarthquakeData> Earthquakes
		=> Node.Children.Where(c => c.Name == Literals.Earthquake()).Select(c => new EarthquakeData(c));

	private Comments? _comments = null;
	/// <summary>
	/// コメント
	/// </summary>
	public Comments? Comments => _comments ??= (Node.TryFindChild(Literals.Comments(), out var n) ? new(n) : null);

	private string? _text = null;
	/// <summary>
	/// 自由文形式で追加的に情報を記載する必要がある場合等
	/// </summary>
	public string? Text => _text ??= (Node.TryFindStringNode(Literals.Text(), out var n) ? n : null);
}
