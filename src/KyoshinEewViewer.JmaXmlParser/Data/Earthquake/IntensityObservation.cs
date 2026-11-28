namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class IntensityObservation<TView>
{
	/// <summary>
	/// コード体系の定義
	/// </summary>
	[JmaXml("CodeDefine?/Type*")]
	public partial JmaXmlList<CodeDefineType<TView>> CodeDefineTypes { get; }

	/// <summary>
	/// 最大震度
	/// </summary>
	[JmaXml("MaxInt?")]
	public partial string? MaxInt { get; }

	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	[JmaXml("MaxLgInt?")]
	public partial string? MaxLgInt { get; }

	/// <summary>
	/// 長周期地震動に関する観測情報の種類<br/>
	/// 1. 全国の最大長周期地震動階級が２以下で、長周期地震動階級１以上が観測されたすべての地域において最大震度が５弱以上である<br/>
	/// 2. 全国の最大長周期地震動階級が２以下で、長周期地震動階級１以上が観測された地域のうち最大震度が４以下となる地域が存在している<br/>
	/// 3. 全国の最大長周期地震動階級が３以上で、長周期地震動階級３以上が観測されたすべての地域において最大震度が５弱以上である<br/>
	/// 4. 全国の最大長周期地震動階級が３以上で、長周期地震動階級３以上が観測された地域のうち最大震度が４以下となる地域が存在している
	/// </summary>
	[JmaXml("LgCategory?")]
	public partial string? LgCategory { get; }

	/// <summary>
	/// 都道府県毎の震度の観測状況
	/// </summary>
	[JmaXml("Pref*")]
	public partial JmaXmlList<Pref<TView>> Prefs { get; }
}
