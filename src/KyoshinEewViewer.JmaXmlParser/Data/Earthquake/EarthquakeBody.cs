using KyoshinEewViewer.JmaXmlParser.Data.Tsunami;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震情報における内容部
/// </summary>
public struct EarthquakeBody(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private TsunamiData? _tsunami = null;
	/// <summary>
	/// 津波に関する情報。存在しない場合は null。
	/// </summary>
	public TsunamiData? Tsunami => _tsunami ??= (Node.TryFindChild("Tsunami"u8, out var n) ? new(n) : null);

	private string? _naming = null;
	/// <summary>
	/// 命名地震<br/>
	/// 顕著な被害を起こした地震について命名した場合、記載される
	/// </summary>
	public string? Naming => _naming ??= (Node.TryFindStringNode(Literals.Naming(), out var n) ? n : null);

	private string? _namingEnglish = null;
	/// <summary>
	/// 命名地震(英語)<br/>
	/// 顕著な被害を起こした地震について命名した場合、記載される
	/// </summary>
	public string? NamingEnglish
	{
		get {
			if (_namingEnglish != null)
				return _namingEnglish;
			if (!Node.TryFindChild(Literals.Naming(), out var n))
				return null;
			if (!n.TryFindStringAttribute(Literals.AttrEnglish(), out var r))
				return null;
			return _namingEnglish = r;
		}
	}

	/// <summary>
	/// 命名地震の英語名。互換性のために残している旧プロパティ。
	/// </summary>
	public string? ForecastCommentText => NamingEnglish;

	/// <summary>
	/// 地震の諸要素の全件。
	/// </summary>
	public IEnumerable<EarthquakeData> Earthquakes
		=> Node.Children.Where(c => c.Name == Literals.Earthquake()).Select(c => new EarthquakeData(c));

	private EarthquakeInfo? _earthquakeInfo = null;
	/// <summary>
	/// 南海トラフ地震に関連する情報、北海道・三陸沖後発地震注意情報。存在しない場合は null。
	/// </summary>
	public EarthquakeInfo? EarthquakeInfo => _earthquakeInfo ??= (Node.TryFindChild("EarthquakeInfo"u8, out var n) ? new(n) : null);

	private EarthquakeInfo? _tokai = null;
	/// <summary>
	/// 旧形式の東海地震関連情報。存在しない場合は null。
	/// </summary>
	public EarthquakeInfo? Tokai => _tokai ??= (Node.TryFindChild("Tokai"u8, out var n) ? new(n) : null);

	private Aftershock? _aftershock = null;
	/// <summary>
	/// 余震の発生確率に関する情報。存在しない場合は null。
	/// </summary>
	public Aftershock? Aftershock => _aftershock ??= (Node.TryFindChild("Aftershock"u8, out var n) ? new(n) : null);

	private EarthquakeCount? _earthquakeCount = null;
	/// <summary>
	/// 地震回数
	/// </summary>
	public EarthquakeCount? EarthquakeCount => _earthquakeCount ??= (Node.TryFindChild(Literals.EarthquakeCount(), out var n) ? new(n) : null);

	private EarthquakeData? _earthquake = null;
	/// <summary>
	/// 地震の諸要素（発生日時、震央地名、震源要素、マグニチュード等）
	/// </summary>
	public EarthquakeData? Earthquake => _earthquake ??= (Node.TryFindChild(Literals.Earthquake(), out var n) ? new(n) : null);

	private Intensity? _intensity = null;
	/// <summary>
	/// 震度・長周期地震動階級に関する情報
	/// </summary>
	public Intensity? Intensity => _intensity ??= (Node.TryFindChild(Literals.Intensity(), out var n) ? new(n) : null);

	private string? _nextAdvisory = null;
	/// <summary>
	/// 次回発表予定<br/>
	/// 続報を発表する予定がある場合は、次回発表予定時刻に関する情報<br/>
	/// EEWでは最終報の場合のみ
	/// </summary>
	public string? NextAdvisory => _nextAdvisory ??= (Node.TryFindStringNode(Literals.NextAdvisory(), out var n) ? n : null);

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
