using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;

/// <summary>
/// 管理部
/// </summary>
public struct ControlMeta
{
	private XmlNode Node { get; set; }

	internal ControlMeta(XmlNode node)
	{
		Node = node;
	}

	private string? _title = null;
	/// <summary>
	/// 包括的に電文の種別を示すための情報名称
	/// <para>種別が同一であれば常に同じ情報名称が記述される。<br/>
	/// 電文の処理系、及び配信系を制御するためのキーとして用いることを想定している</para>
	/// </summary>
	public string Title => _title ??= (Node.TryFindStringNode(Literals.Title(), out var n) ? n : throw new JmaXmlParseException("Title ノードが存在しません"));

	private DateTimeOffset? _datetime = null;
	/// <summary>
	/// 電文を作成、発信した実時刻
	/// <para>順序や同一性を検証するためのキーとして用いることを想定している</para>
	/// </summary>
	public DateTimeOffset DateTime => _datetime ??= (Node.TryFindDateTimeNode(Literals.DateTime(), out var n) ? n : throw new JmaXmlParseException("DateTime ノードが存在しません"));

	private string? _status = null;
	/// <summary>
	/// 電文の運用上の種別<br/>
	/// 原則として 2形態の表記法により表現する
	/// <list type="number">
	///		<item>
	///			「通常」「訓練」「試験」等の日本語形式
	///			<list type="bullet">
	///				<item>通常: 通常の運用で発表する情報</item>
	///				<item>訓練: 事前に日時を定めて行う業務訓練等で発表する情報</item>
	///				<item>試験: 定期または臨時に電文疎通確認等を目的として発表する情報</item>
	///			</list>
	///		</item>
	///		<item>
	///			「CCA」等の英字形式<br/>
	///			現状の WMO の GTS 配信に則った運用
	///		</item>
	/// </list>
	/// どちらの形式により表現するかは、情報名称により一意に定まる
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=6"/></para>
	/// </summary>
	public string Status => _status ??= (Node.TryFindStringNode(Literals.Status(), out var n) ? n : throw new JmaXmlParseException("Status ノードが存在しません"));

	private string? _editorialOffice = null;
	/// <summary>
	/// 電文を作成した機関(発信処理に関わった機関名称)<br/>
	/// 配信系で制御のキーとして用いることを想定している
	/// <para>地震・津波に関連する情報、南海トラフ地震に関連する情報及び地震・津波に関するお知らせについては、システム障害発生等により一連の情報であっても編集官署が切り替わる場合がある</para>
	/// </summary>
	public string EditorialOffice => _editorialOffice ??= (Node.TryFindStringNode(Literals.EditorialOffice(), out var n) ? n : throw new JmaXmlParseException("EditorialOffice ノードが存在しません"));

	private string? _publishingOffice = null;
	/// <summary>
	/// 業務的に電文の作成に責任を持っている機関、発表官の署名
	/// 配信系で制御のキーとして用いる際は <see cref="EditorialOffice"/> を使用する
	/// </summary>
	public string PublishingOffice => _publishingOffice ??= (Node.TryFindStringNode(Literals.PublishingOffice(), out var n) ? n : throw new JmaXmlParseException("PublishingOffice ノードが存在しません"));
}
