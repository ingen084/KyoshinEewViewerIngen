using KyoshinEewViewer.JmaXmlParser.Data.Headline;
using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;

/// <summary>
/// ヘッダ部
/// </summary>
public class HeadData
{
	private XmlNode Node { get; set; }

	internal HeadData(XmlNode node)
	{
		Node = node;
	}

	private string? title;
	/// <summary>
	/// 電文の概要を示す標題について、人間可読的な情報
	/// <para>管理部とは異なる値の場合があるため、電文の処理系、及び配信系を制御するためのキーとして利用する場合は <seealso cref="ControlMeta.Title"/> を使用すること</para>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=7"/></para>
	/// </summary>
	public string Title => title ??= (Node.TryFindStringNode(Literals.Title(), out var n) ? n : throw new JmaXmlParseException("Title ノードが存在しません"));

	private DateTimeOffset? reportDateTime;
	/// <summary>
	/// 電文の公式な発表時刻
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	public DateTimeOffset ReportDateTime => reportDateTime ??= (Node.TryFindDateTimeNode(Literals.ReportDateTime(), out var n) ? n : throw new JmaXmlParseException("ReportDateTime ノードが存在しません"));

	private DateTimeOffset? targetDateTime;
	/// <summary>
	/// 電文の内容について発現、発効する基点時刻
	/// <para>具体的には、観測情報の場合は観測した時刻を、予報情報の場合は予報対象時刻の基点時刻を示す場合など。</para>
	/// 基点時刻が精度的表現に対して不適切な場合 <c>null</c>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	public DateTimeOffset? TargetDateTime => targetDateTime ??= (Node.TryFindNullableDateTimeNode(Literals.TargetDateTime(), out var n) ? n : throw new JmaXmlParseException("TargetDateTime ノードが存在しません"));

	private string? targetDuration;
	/// <summary>
	/// 電文の内容について対象となる期間を必要に応じて示す場合の、基点時刻からの時間幅<br/>
	/// 値が存在しない場合は <c>null</c><br/>
	/// 例: <c>PT72H</c>
	/// </summary>
	public string? TargetDuration => targetDuration ??= (Node.TryFindStringNode(Literals.TargetDuration(), out var n) ? n : null);

	private string? targetDTDubious;
	/// <summary>
	/// 時刻情報に対する基点時刻の精度としてのあいまいさ<br/>
	/// 値が存在しない場合は <c>null</c><br/>
	/// 例: <c>頃</c>、<c>秒頃</c>、<c>分頃</c>～<c>年頃</c>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	public string? TargetDTDubious => targetDTDubious ??= (Node.TryFindStringNode(Literals.TargetDTDubious(), out var n) ? n : null);

	private DateTimeOffset? validDateTime;
	/// <summary>
	/// 電文の内容について無効となる時刻<br/>
	/// 存在しない場合は <c>null</c>
	/// <para>現在時刻が当該時刻に達した時点で、当該電文の情報は無効となる</para>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	public DateTimeOffset? ValidDateTime => validDateTime ??= (Node.TryFindDateTimeNode(Literals.ValidDateTime(), out var n) ? n : null);

	private string? eventId;
	/// <summary>
	/// 情報を詳細に判別するためのキー
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=9"/></para>
	/// </summary>
	public string EventId => eventId ??= (Node.TryFindStringNode(Literals.EventId(), out var n) ? n : throw new JmaXmlParseException("EventID ノードが存在しません"));

	private string? infoType;
	/// <summary>
	/// 情報の種類<br/>
	/// 「発表」、「更新」、「訂正」、「取消」があり、電文の情報形態に応じて設定される
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=9"/></para>
	/// </summary>
	public string InfoType => infoType ??= (Node.TryFindStringNode(Literals.InfoType(), out var n) ? n : throw new JmaXmlParseException("InfoType ノードが存在しません"));

	private string? serial;
	/// <summary>
	/// 情報の報数<br/>
	/// 第一報から順番に番号を一つずつ増加させる運用が基本
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=9"/></para>
	/// </summary>
	public string Serial => serial ??= (Node.TryFindStringNode(Literals.Serial(), out var n) ? n : throw new JmaXmlParseException("Serial ノードが存在しません"));

	private string? infoKind;
	/// <summary>
	/// 同一スキーマにて表現可能な情報において、その情報別の運用を示すための種別情報
	/// <para>同一スキーマの情報であっても、スキーマ上の定義として任意の出現回数にて定義されている要素、属性については運用が異なる(例:気象警報と気象予報)<br/>
	/// このような狭義に限定された情報種別におけるスキーマの運用を示す</para>
	/// </summary>
	public string InfoKind => infoKind ??= (Node.TryFindStringNode(Literals.InfoKind(), out var n) ? n : throw new JmaXmlParseException("InfoKind ノードが存在しません"));

	private string? infoKindVersion;
	/// <summary>
	/// 同一のスキーマ運用種別における運用バージョン
	/// <para>詳細は <seealso href="https://xml.kishou.go.jp/jmaxml_20160331_format_v1_2.pdf#page=19"/></para>
	/// </summary>
	public string InfoKindVersion => infoKindVersion ??= (Node.TryFindStringNode(Literals.InfoKindVersion(), out var n) ? n : throw new JmaXmlParseException("InfoKindVersion ノードが存在しません"));


	private HeadlineData? headline;
	/// <summary>
	/// Headline を取得する
	/// <para>(地震火山) <seealso href="https://dmdata.jp/doc/jma/manual/0101-0183.pdf#page=11"/></para>
	/// </summary>
	public HeadlineData Headline => headline ??= (Node.TryFindChild(Literals.Headline(), out var n) ? new(n) : throw new JmaXmlParseException("Headline ノードが存在しません"));
}
