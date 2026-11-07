using System;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

public struct WindPart(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private DateTimeOffset? _time = null;
	/// <summary>
	/// 風の情報の対象時刻。存在しない場合は null。
	/// </summary>
	public DateTimeOffset? Time => _time ??= (Node.TryFindDateTimeNode("Time"u8, out var n) ? n : null);

	private string? _remark = null;
	/// <summary>
	/// 風の情報についての補足情報。存在しない場合は null。
	/// </summary>
	public string? Remark => _remark ??= (Node.TryFindStringNode(Literals.Remark(), out var n) ? n : null);

	/// <summary>
	/// 予報円の中心位置<br/>
	/// type が 最大風速 の場合、 condition は、値が "中心付近"、"中心付近を除く"、"なし" の場合がある
	/// </summary>
	public IEnumerable<PhysicalQuantity> WindSpeeds
		=> Node.Children.Where(c => c.IsElementBasis("WindSpeed"u8)).Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 風向。
	/// </summary>
	public IEnumerable<PhysicalQuantity> WindDirections
		=> Node.Children.Where(c => c.IsElementBasis("WindDirection"u8)).Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 風向の角度。
	/// </summary>
	public IEnumerable<PhysicalQuantity> WindDegrees
		=> Node.Children.Where(c => c.IsElementBasis("WindDegree"u8)).Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 風力階級。
	/// </summary>
	public IEnumerable<PhysicalQuantity> WindScales
		=> Node.Children.Where(c => c.IsElementBasis("WindScale"u8)).Select(c => new PhysicalQuantity(c));
}
