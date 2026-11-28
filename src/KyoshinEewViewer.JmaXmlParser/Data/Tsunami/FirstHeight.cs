using System;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class FirstHeight<TView>
{
	/// <summary>
	/// 当該津波予報区への第1波の到達予想時刻
	/// </summary>
	[JmaXml("ArrivalTime?")]
	public partial DateTimeOffset? ArrivalTime { get; }

	/// <summary>
	/// 水位が上昇中の場合 "上昇中"
	/// </summary>
	[JmaXml("Condition?")]
	public partial string? Condition { get; }

	/// <summary>
	/// 観測した津波の極性<br/>
	/// 押し or 引き
	/// </summary>
	[JmaXml("Initial?")]
	public partial string? Initial { get; }

	/// <summary>
	/// 新たに出現した場合は 追加<br/>
	/// 既出かつ更新された場合は 更新
	/// </summary>
	[JmaXml("Revise?")]
	public partial string? Revise { get; }
}
