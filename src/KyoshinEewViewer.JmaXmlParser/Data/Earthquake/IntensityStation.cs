namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class IntensityStation<TView>
{
	/// <summary>
	/// 観測点名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 観測点コード
	/// </summary>
	[JmaXml("Code!")]
	public partial string Code { get; }

	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない市町村のみしか存在しない場合 震度５弱以上未入電 になる
	/// <para>「リアルタイム震度電文」では、リアルタイム震度（工学的基盤面の値）の震度階級になる</para>
	/// </summary>
	[JmaXml("Int!")]
	public partial string Int { get; }

	/// <summary>
	/// リアルタイム震度（工学的基盤面の値）の計測震度<br/>
	/// 「リアルタイム震度電文」以外は null
	/// </summary>
	[JmaXml("K?")]
	public partial float? K { get; }

	/// <summary>
	/// 情報の更新（地域）<br/>
	/// 当該観測点が新規に追加または更新される場合以外は null<br/>
	/// 例: 追加、上方修正、下方修正
	/// </summary>
	[JmaXml("Revise?")]
	public partial string? Revise { get; }

	/// <summary>
	/// 絶対速度応答スペクトルの 1.6 秒から 7.8 秒までの周期帯における最大値
	/// </summary>
	[JmaXml("Sva?")]
	public partial Sva<TView>? Sva { get; }

	/// <summary>
	/// 各周期毎の長周期地震動階級の観測状況
	/// </summary>
	[JmaXml("LgIntPerPeriod*")]
	public partial JmaXmlList<LgIntPerPeriod<TView>> LgIntPerPeriods { get; }

	/// <summary>
	/// 各周期毎の加速度
	/// </summary>
	[JmaXml("SvaPerPeriod*")]
	public partial JmaXmlList<SvaPerPeriod<TView>> SvaPerPeriods { get; }
}
