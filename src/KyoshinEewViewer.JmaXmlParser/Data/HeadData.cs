using KyoshinEewViewer.JmaXmlParser.Data.Headline;
using System;

namespace KyoshinEewViewer.JmaXmlParser.Data;

/// <summary>
/// ヘッダ部
/// </summary>
[JmaXmlType]
public sealed partial class HeadData<TView>
{
	/// <summary>
	/// 電文の概要を示す標題について、人間可読的な情報
	/// <para>管理部とは異なる値の場合があるため、電文の処理系、及び配信系を制御するためのキーとして利用する場合は <seealso cref="ControlMeta.Title"/> を使用すること</para>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=7"/></para>
	/// <para>(台風) <seealso href="https://dmdata.jp/docs/jma/manual/0266-0267.pdf#page=7"/></para>
	/// </summary>
	[JmaXml("Title!")]
	public partial string Title { get; }

	/// <summary>
	/// 電文の公式な発表時刻
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	[JmaXml("ReportDateTime!")]
	public partial DateTimeOffset ReportDateTime { get; }

	/// <summary>
	/// 電文の内容について発現、発効する基点時刻
	/// <para>具体的には、観測情報の場合は観測した時刻を、予報情報の場合は予報対象時刻の基点時刻を示す場合など。</para>
	/// 基点時刻が精度的表現に対して不適切な場合 <c>null</c>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	[JmaXml("TargetDateTime!", Nil = true)]
	public partial DateTimeOffset? TargetDateTime { get; }

	/// <summary>
	/// 電文の内容について対象となる期間を必要に応じて示す場合の、基点時刻からの時間幅<br/>
	/// 値が存在しない場合は <c>null</c><br/>
	/// 例: <c>PT72H</c>
	/// </summary>
	[JmaXml("TargetDuration?")]
	public partial string? TargetDuration { get; }

	/// <summary>
	/// 時刻情報に対する基点時刻の精度としてのあいまいさ<br/>
	/// 値が存在しない場合は <c>null</c><br/>
	/// 例: <c>頃</c>、<c>秒頃</c>、<c>分頃</c>～<c>年頃</c>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	[JmaXml("TargetDTDubious?")]
	public partial string? TargetDtDubious { get; }

	/// <summary>
	/// 電文の内容について無効となる時刻<br/>
	/// 存在しない場合は <c>null</c>
	/// <para>現在時刻が当該時刻に達した時点で、当該電文の情報は無効となる</para>
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=8"/></para>
	/// </summary>
	[JmaXml("ValidDateTime?")]
	public partial DateTimeOffset? ValidDateTime { get; }

	/// <summary>
	/// 情報を詳細に判別するためのキー
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=9"/></para>
	/// </summary>
	[JmaXml("EventID!")]
	public partial string EventId { get; }

	/// <summary>
	/// 情報の種類<br/>
	/// 「発表」、「更新」、「訂正」、「取消」があり、電文の情報形態に応じて設定される
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=9"/></para>
	/// </summary>
	[JmaXml("InfoType!")]
	public partial string InfoType { get; }

	/// <summary>
	/// 情報の報数<br/>
	/// 第一報から順番に番号を一つずつ増加させる運用が基本
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=9"/></para>
	/// </summary>
	[JmaXml("Serial!")]
	public partial string Serial { get; }

	/// <summary>
	/// 同一スキーマにて表現可能な情報において、その情報別の運用を示すための種別情報
	/// <para>同一スキーマの情報であっても、スキーマ上の定義として任意の出現回数にて定義されている要素、属性については運用が異なる(例:気象警報と気象予報)<br/>
	/// このような狭義に限定された情報種別におけるスキーマの運用を示す</para>
	/// </summary>
	[JmaXml("InfoKind!")]
	public partial string InfoKind { get; }

	/// <summary>
	/// 同一のスキーマ運用種別における運用バージョン
	/// <para>詳細は <seealso href="https://xml.kishou.go.jp/jmaxml_20160331_format_v1_2.pdf#page=19"/></para>
	/// </summary>
	[JmaXml("InfoKindVersion!")]
	public partial string InfoKindVersion { get; }

	/// <summary>
	/// Headline を取得する
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=11"/></para>
	/// </summary>
	[JmaXml("Headline!")]
	public partial HeadlineData<TView> Headline { get; }
}
