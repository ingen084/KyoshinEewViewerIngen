namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class CenterPart<TView>
{
	/// <summary>
	/// 台風の中心位置<br/>
	/// 「実況」及び「推定 １時間後」の場合のみ出現
	/// </summary>
	[JmaXml("jmx_eb:Coordinate*")]
	public partial JmaXmlList<Coordinate<TView>> Coordinates { get; }

	/// <summary>
	/// 予報円<br/>
	/// 「予報 X 時間後」及び「延長予報 X 時間後」の場合のみ出現
	/// </summary>
	[JmaXml("ProbabilityCircle?")]
	public partial TyphoonCircle<TView>? ProbabilityCircle { get; }

	/// <summary>
	/// 台風の存在域<br/>
	/// 例: 与論島の西北西約 80km
	/// </summary>
	[JmaXml("Location?")]
	public partial string? Location { get; }

	/// <summary>
	/// 台風の移動方向<br/>
	/// 空の場合、 condition="不定"、description="不定" となる
	/// </summary>
	[JmaXml("jmx_eb:Direction!")]
	public partial PhysicalQuantity<TView> Direction { get; }

	/// <summary>
	/// 台風の移動速度
	/// </summary>
	[JmaXml("jmx_eb:Speed*")]
	public partial JmaXmlList<PhysicalQuantity<TView>> Speeds { get; }

	/// <summary>
	/// 台風の中心気圧
	/// </summary>
	[JmaXml("jmx_eb:Pressure!")]
	public partial PhysicalQuantity<TView> Pressure { get; }
}
