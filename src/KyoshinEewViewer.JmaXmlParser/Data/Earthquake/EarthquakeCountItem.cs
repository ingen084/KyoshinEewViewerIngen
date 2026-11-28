using System;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class EarthquakeCountItem<TView>
{
	/// <summary>
	/// 期間幅の種類<br/>
	/// "１時間地震回数"（1 時間単位）、"累積地震回数"（全期間の合計）、"地震回数"（その他の場合）
	/// </summary>
	[JmaXml("@type!")]
	public partial string Type { get; }

	/// <summary>
	/// 開始時刻
	/// </summary>
	[JmaXml("StartTime!")]
	public partial DateTimeOffset StartTime { get; }

	/// <summary>
	///終了時刻
	/// </summary>
	[JmaXml("EndTime!")]
	public partial DateTimeOffset EndTime { get; }

	/// <summary>
	/// 無感地震を含む全ての地震回数<br/>
	/// 発表しない場合は -1
	/// </summary>
	[JmaXml("Number!")]
	public partial string Number { get; }

	/// <summary>
	/// 有感地震回数<br/>
	/// 発表しない場合は -1
	/// </summary>
	[JmaXml("FeltNumber!")]
	public partial string FeltNumber { get; }
}
