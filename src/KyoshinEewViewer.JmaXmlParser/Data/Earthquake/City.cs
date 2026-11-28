namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class City<TView>
{
	/// <summary>
	/// 市町村名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 市町村コード
	/// </summary>
	[JmaXml("Code!")]
	public partial int Code { get; }

	/// <summary>
	/// 最大震度の状態<br/>
	/// 基本は null<br/>
	/// 基準となる震度以上と考えられるが震度の値を入手していない震度観測点が存在し、その他の観測点の最大震度が基準となる震度未満または入電なしの場合に記載される<br/>
	/// （当面は震度５弱以上となっているため、 震度５弱以上未入電 が入る）
	/// </summary>
	[JmaXml("Condition?")]
	public partial string? Condition { get; }

	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない震度観測点のみしか存在しない場合 null<br/>
	/// 基準となる震度未満の観測点が最大震度となっているが、基準となる震度以上と考えられるが震度の値を入手していない震度観測点が存在する場合、 <seealso cref="Condition"/> に値がセットされる
	/// </summary>
	[JmaXml("MaxInt?")]
	public partial string? MaxInt { get; }

	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	[JmaXml("MaxLgInt?")]
	public partial string? MaxLgInt { get; }

	/// <summary>
	/// 情報の更新（地域）<br/>
	/// 当該市町村が新規に追加または更新される場合以外は null<br/>
	/// 例: 追加、上方修正
	/// </summary>
	[JmaXml("Revise?")]
	public partial string? Revise { get; }

	/// <summary>
	/// 観測点毎の震度の観測状況
	/// </summary>
	[JmaXml("IntensityStation*")]
	public partial JmaXmlList<IntensityStation<TView>> IntensityStations { get; }
}
