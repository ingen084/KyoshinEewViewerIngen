using System;

namespace KyoshinEewViewer.JmaXmlParser.Data;

/// <summary>
/// 管理部
/// </summary>
[JmaXmlType]
public sealed partial class ControlMeta<TView>
{
	/// <summary>
	/// 包括的に電文の種別を示すための情報名称
	/// <para>種別が同一であれば常に同じ情報名称が記述される。<br/>
	/// 電文の処理系、及び配信系を制御するためのキーとして用いることを想定している</para>
	/// </summary>
	[JmaXml("Title!")]
	public partial string Title { get; }

	/// <summary>
	/// 電文を作成、発信した実時刻
	/// <para>順序や同一性を検証するためのキーとして用いることを想定している</para>
	/// </summary>
	[JmaXml("DateTime!")]
	public partial DateTimeOffset DateTime { get; }

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
	[JmaXml("Status!")]
	public partial string Status { get; }

	/// <summary>
	/// 電文を作成した機関(発信処理に関わった機関名称)<br/>
	/// 配信系で制御のキーとして用いることを想定している
	/// <para>地震・津波に関連する情報、南海トラフ地震に関連する情報及び地震・津波に関するお知らせについては、システム障害発生等により一連の情報であっても編集官署が切り替わる場合がある</para>
	/// </summary>
	[JmaXml("EditorialOffice!")]
	public partial string EditorialOffice { get; }

	/// <summary>
	/// 業務的に電文の作成に責任を持っている機関、発表官の署名
	/// 配信系で制御のキーとして用いる際は <see cref="EditorialOffice"/> を使用する
	/// </summary>
	[JmaXml("PublishingOffice!")]
	public partial string PublishingOffice { get; }
}
