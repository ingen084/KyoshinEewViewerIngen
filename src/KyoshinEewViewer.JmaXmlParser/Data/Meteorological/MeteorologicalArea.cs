using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 気象情報の対象地域。地域コードは文字列として保持する。
/// </summary>
public struct MeteorologicalArea(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 地域名。
	/// </summary>
	public string Name => Node.TryFindStringNode("Name"u8, out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません");

	/// <summary>
	/// 地域コード。先頭のゼロを保持する。省略時は null。
	/// </summary>
	public string? Code => Node.TryFindStringNode("Code"u8, out var n) ? n : null;

	/// <summary>
	/// 地域コードの体系。存在しない場合は null。
	/// </summary>
	public string? CodeType => Node.TryFindStringAttribute("codeType"u8, out var n) ? n : null;

	/// <summary>
	/// 都道府県名。存在しない場合は null。
	/// </summary>
	public string? Prefecture => Node.TryFindStringNode("Prefecture"u8, out var n) ? n : null;

	/// <summary>
	/// 都道府県コード。存在しない場合は null。
	/// </summary>
	public string? PrefectureCode => Node.TryFindStringNode("PrefectureCode"u8, out var n) ? n : null;

	/// <summary>
	/// 都道府県名のリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? PrefectureList => Node.TryFindStringNode("PrefectureList"u8, out var n) ? n : null;

	/// <summary>
	/// 都道府県コードのリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? PrefectureCodeList => Node.TryFindStringNode("PrefectureCodeList"u8, out var n) ? n : null;

	/// <summary>
	/// 府県内の地域名。存在しない場合は null。
	/// </summary>
	public string? SubPrefecture => Node.TryFindStringNode("SubPrefecture"u8, out var n) ? n : null;

	/// <summary>
	/// 府県内の地域コード。存在しない場合は null。
	/// </summary>
	public string? SubPrefectureCode => Node.TryFindStringNode("SubPrefectureCode"u8, out var n) ? n : null;

	/// <summary>
	/// 府県内の地域名のリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? SubPrefectureList => Node.TryFindStringNode("SubPrefectureList"u8, out var n) ? n : null;

	/// <summary>
	/// 府県内の地域コードのリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? SubPrefectureCodeList => Node.TryFindStringNode("SubPrefectureCodeList"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村名。存在しない場合は null。
	/// </summary>
	public string? City => Node.TryFindStringNode("City"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村コード。存在しない場合は null。
	/// </summary>
	public string? CityCode => Node.TryFindStringNode("CityCode"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村名のリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? CityList => Node.TryFindStringNode("CityList"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村コードのリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? CityCodeList => Node.TryFindStringNode("CityCodeList"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村内の地域名。存在しない場合は null。
	/// </summary>
	public string? SubCity => Node.TryFindStringNode("SubCity"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村内の地域コード。存在しない場合は null。
	/// </summary>
	public string? SubCityCode => Node.TryFindStringNode("SubCityCode"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村内の地域名のリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? SubCityList => Node.TryFindStringNode("SubCityList"u8, out var n) ? n : null;

	/// <summary>
	/// 市町村内の地域コードのリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? SubCityCodeList => Node.TryFindStringNode("SubCityCodeList"u8, out var n) ? n : null;

	/// <summary>
	/// 地域コードのリスト。リストは空白区切りの原文。存在しない場合は null。
	/// </summary>
	public string? CodeList => Node.TryFindStringNode("CodeList"u8, out var n) ? n : null;

	/// <summary>
	/// 地域の説明。存在しない場合は null。
	/// </summary>
	public string? Location => Node.TryFindStringNode("Location"u8, out var n) ? n : null;

	/// <summary>
	/// 地域の状態。存在しない場合は null。
	/// </summary>
	public string? Status => Node.TryFindStringNode("Status"u8, out var n) ? n : null;

	/// <summary>
	/// Prefectureの一部地域の区分。存在しない場合は null。
	/// </summary>
	public string? PrefecturePartType => Node.TryFindChild("Prefecture"u8, out var n) && n.TryFindStringAttribute("partType"u8, out var value) ? value : null;

	/// <summary>
	/// Cityの一部地域の区分。存在しない場合は null。
	/// </summary>
	public string? CityPartType => Node.TryFindChild("City"u8, out var n) && n.TryFindStringAttribute("partType"u8, out var value) ? value : null;

	/// <summary>
	/// 対象地域の形状。存在しない場合は空。
	/// </summary>
	public IEnumerable<Coordinate> Coordinates
		=> Node.Children.Where(c => c.IsElementBasis("Coordinate"u8)).Select(c => new Coordinate(c));

	/// <summary>
	/// 対象地域の形状。存在しない場合は空。
	/// </summary>
	public IEnumerable<TyphoonCircle> Circles
		=> Node.Children.Where(c => c.IsElementBasis("Circle"u8)).Select(c => new TyphoonCircle(c));

	/// <summary>
	/// 対象地域の形状。存在しない場合は空。
	/// </summary>
	public IEnumerable<Coordinate> Lines
		=> Node.Children.Where(c => c.IsElementBasis("Line"u8)).Select(c => new Coordinate(c));

	/// <summary>
	/// 対象地域の形状。存在しない場合は空。
	/// </summary>
	public IEnumerable<Coordinate> Polygons
		=> Node.Children.Where(c => c.IsElementBasis("Polygon"u8)).Select(c => new Coordinate(c));
}
