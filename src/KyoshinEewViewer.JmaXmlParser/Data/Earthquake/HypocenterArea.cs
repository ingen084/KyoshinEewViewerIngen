using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct HypocenterArea
{
	private XmlNode Node { get; set; }

	public HypocenterArea(XmlNode node)
	{
		Node = node;
	}

	private string? _name = null;
	/// <summary>
	/// 震央地名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Area ノードが存在しません"));

	private string? _code = null;
	/// <summary>
	/// 震央地名コード
	/// </summary>
	public string Code => _code ??= (Node.TryFindStringNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));

	private Coordinate? _coordinate = null;
	/// <summary>
	/// 震源要素(複数ある場合は1件目のみ)
	/// </summary>
	public Coordinate Coordinate => _coordinate ??= (Node.TryFindChild(Literals.JmxEbCoordinate(), out var n) ? new Coordinate(n) : throw new JmaXmlParseException("Coordinate ノードが存在しません"));

	/// <summary>
	/// 震源要素<br/>
	/// 顕著な地震の震源要素更新のお知らせ の場合は1件以上となる
	/// </summary>
	public IEnumerable<Coordinate> Coordinates
		=> Node.Children.Where(c => c.Name == Literals.JmxEbCoordinate()).Select(c => new Coordinate(c));

	private string? _reduceName = null;
	/// <summary>
	/// 短縮用震央地名<br/>
	/// EEW以外は null
	/// </summary>
	public string? ReduceName => _reduceName ??= (Node.TryFindStringNode(Literals.ReduceName(), out var n) ? n : null);

	private string? _reduceCode = null;
	/// <summary>
	/// 短縮用震央地名コード<br/>
	/// EEW以外は null
	/// </summary>
	public string? ReduceCode => _reduceCode ??= (Node.TryFindStringNode(Literals.ReduceCode(), out var n) ? n : null);

	private string? _landOrSea = null;
	/// <summary>
	/// 内陸判定<br/>
	/// 例: 内陸、海域<br/>
	/// 以下の場合は null
	/// <list type="bullet">
	///		<item>EEW以外</item>
	///		<item>「緊急地震速報（地震動予報）」、および「緊急地震速報（予報）」において、非常に強い揺れを検知・最大予測震度のみの場合</item>
	///		<item>「仮定震源要素」の場合</item>
	/// </list>
	/// は null
	/// </summary>
	public string? LandOrSea => _landOrSea ??= (Node.TryFindStringNode(Literals.LandOrSea(), out var n) ? n : null);

	private string? _detailedName = null;
	/// <summary>
	/// 詳細震央地名<br/>
	/// 国外で発生した地震について、震源地の詳細な位置
	/// </summary>
	public string? DetailedName => _detailedName ??= (Node.TryFindStringNode(Literals.DetailedName(), out var n) ? n : null);

	private string? _detailedCode = null;
	/// <summary>
	/// 詳細震央地名コード<br/>
	/// コード種別 は 詳細震央地名
	/// </summary>
	public string? DetailedCode => _detailedCode ??= (Node.TryFindStringNode(Literals.DetailedCode(), out var n) ? n : null);

	private string? _nameFromMark = null;
	/// <summary>
	/// 震央補助表現<br/>
	/// 日本近海で発生し、津波警報・注意報を発表した地震について、震源地の詳細な位置を示すための目印となる地名
	/// </summary>
	public string? NameFromMark => _nameFromMark ??= (Node.TryFindStringNode(Literals.NameFromMark(), out var n) ? n : null);

	private string? _source = null;
	/// <summary>
	/// 震源決定機関<br/>
	/// 国外で発生した地震について、気象庁以外の機関で決定された震源要素を採用して情報発表した場合<br/>
	/// 例: PTWC、WCATWC、USGS
	/// </summary>
	public string? Source => _source ??= (Node.TryFindStringNode(Literals.Source(), out var n) ? n : null);
}
