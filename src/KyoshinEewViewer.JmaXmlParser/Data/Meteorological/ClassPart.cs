using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct ClassPart(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _typhoonClass = null;
	/// <summary>
	/// 台風の階級
	/// <list type="bullet">
	/// <item>台風（TY）</item>
	/// <item>台風（STS）</item>
	/// <item>台風（TS）</item>
	/// <item>熱帯低気圧（TD）</item>
	/// <item>ハリケーン（Hurricane）</item>
	/// <item>発達した熱帯低気圧（Tropical Storm）</item>
	/// <item>温帯低気圧（LOW）</item>
	/// </list>
	/// 「延長予報 X 時間後」では記述なし
	/// </summary>
	public string? TyphoonClass => _typhoonClass ??= (Node.TryFindStringNode(Literals.JmaEbTyphoonClass(), out var n) ? n : null);

	private string? _areaClass = null;
	/// <summary>
	/// 台風の大きさ
	/// <list type="bullet">
	/// <item>大型</item>
	/// <item>超大型</item>
	/// </list>
	/// 「予報 X 時間後」及び「延長予報 X 時間後」では省略する
	/// </summary>
	public string? AreaClass => _areaClass ??= (Node.TryFindStringNode(Literals.JmaEbAreaClass(), out var n) ? n : null);

	private string? _intensityClass = null;
	/// <summary>
	/// 台風の強さ
	/// <list type="bullet">
	/// <item>強い</item>
	/// <item>非常に強い</item>
	/// <item>猛烈な</item>
	/// </list>
	/// 「延長予報 X 時間後」では省略
	/// </summary>
	public string? IntensityClass => _intensityClass ??= (Node.TryFindStringNode(Literals.JmaEbIntensityClass(), out var n) ? n : null);
}
