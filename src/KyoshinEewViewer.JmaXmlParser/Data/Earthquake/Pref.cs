namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震情報における都道府県
/// </summary>
[JmaXmlType]
public sealed partial class Pref<TView>
{
	/// <summary>
	/// 都道府県名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 都道府県コード
	/// </summary>
	[JmaXml("Code!")]
	public partial int Code { get; }

	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない市町村のみしか存在しない場合 null
	/// </summary>
	[JmaXml("MaxInt?")]
	public partial string? MaxInt { get; }

	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	[JmaXml("MaxLgInt?")]
	public partial string? MaxLgInt { get; }

	/// <summary>
	/// 情報の更新（都道府県）<br/>
	/// 当該都道府県が新規に追加されるか更新される場合以外は null<br/>
	/// 例: 追加、上方修正
	/// </summary>
	[JmaXml("Revise?")]
	public partial string? Revise { get; }

	/// <summary>
	/// 地域毎の震度の観測状況
	/// </summary>
	[JmaXml("Area*")]
	public partial JmaXmlList<Area<TView>> Areas { get; }
}
