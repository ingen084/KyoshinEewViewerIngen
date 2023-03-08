using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震情報における内容部
/// </summary>
public struct EarthquakeBody
{
	private XmlNode Node { get; set; }

	public EarthquakeBody(XmlNode node)
	{
		Node = node;
	}

	private string? naming = null;
	/// <summary>
	/// 命名地震<br/>
	/// 顕著な被害を起こした地震について命名した場合、記載される
	/// </summary>
	public string? Naming => naming ??= (Node.TryFindStringNode(Literals.Naming(), out var n) ? n : null);

	private string? namingEnglish = null;
	/// <summary>
	/// 命名地震(英語)<br/>
	/// 顕著な被害を起こした地震について命名した場合、記載される
	/// </summary>
	public string? ForecastCommentText
	{
		get {
			if (namingEnglish != null)
				return namingEnglish;
			if (!Node.TryFindChild(Literals.Naming(), out var n))
				return null;
			if (!n.TryFindStringAttribute(Literals.AttrEnglish(), out var r))
				return null;
			return namingEnglish = r;
		}
	}

	private EarthquakeCount? earthquakeCount = null;
	/// <summary>
	/// 地震回数
	/// </summary>
	public EarthquakeCount? EarthquakeCount => earthquakeCount ??= (Node.TryFindChild(Literals.EarthquakeCount(), out var n) ? new(n) : null);

	private EarthquakeData? earthquake = null;
	/// <summary>
	/// 地震の諸要素（発生日時、震央地名、震源要素、マグニチュード等）
	/// </summary>
	public EarthquakeData? Earthquake => earthquake ??= (Node.TryFindChild(Literals.Earthquake(), out var n) ? new(n) : null);

	private Intensity? intensity = null;
	/// <summary>
	/// 震度・長周期地震動階級に関する情報
	/// </summary>
	public Intensity? Intensity => intensity ??= (Node.TryFindChild(Literals.Intensity(), out var n) ? new(n) : null);

	private string? nextAdvisory = null;
	/// <summary>
	/// 次回発表予定<br/>
	/// 続報を発表する予定がある場合は、次回発表予定時刻に関する情報<br/>
	/// EEWでは最終報の場合のみ
	/// </summary>
	public string? NextAdvisory => nextAdvisory ??= (Node.TryFindStringNode(Literals.NextAdvisory(), out var n) ? n : null);

	private Comments? comments = null;
	/// <summary>
	/// コメント
	/// </summary>
	public Comments? Comments => comments ??= (Node.TryFindChild(Literals.Comments(), out var n) ? new(n) : null);

	private string? text = null;
	/// <summary>
	/// 自由文形式で追加的に情報を記載する必要がある場合等
	/// </summary>
	public string? Text => text ??= (Node.TryFindStringNode(Literals.Text(), out var n) ? n : null);
}
