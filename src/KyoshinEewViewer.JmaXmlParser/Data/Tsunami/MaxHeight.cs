using System;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class MaxHeight<TView>
{
	/// <summary>
	/// 観測時刻
	/// </summary>
	[JmaXml("DateTime?")]
	public partial DateTimeOffset? DateTime { get; }

	/// <summary>
	/// 予想される津波の高さ<br/>
	/// マグニチュードが 8 を超える巨大地震と推定されるなど、地震規模推定の不確定性が大きい場合は condition=不明 高さは NaN になる
	/// また、 description に定性的表現(巨大/高い) 津波予報･注意報の場合は空
	/// </summary>
	[JmaXml("jmx_eb:TsunamiHeight?")]
	public partial PhysicalQuantity<TView>? TsunamiHeight { get; }

	/// <summary>
	/// 観測されたこれまでの最大波が非常に小さい場合、TsunamiHeight の代わりに "微弱"<br/>
	/// 津波警報以上の津波予報区において、観測されたこれまでの最大波の高さが予想される高さに比べて十分小さい場合、TsunamiHeight の代わりに"観測中"<br/>
	/// これまでの最大波の高さが大津波警報の基準を超え、追加あるいは更新された場合は、"重要"
	/// </summary>
	[JmaXml("Condition?")]
	public partial string? Condition { get; }

	/// <summary>
	/// 新たに出現した場合は 追加<br/>
	/// 既出かつ更新された場合は 更新
	/// </summary>
	[JmaXml("Revise?")]
	public partial string? Revise { get; }
}
