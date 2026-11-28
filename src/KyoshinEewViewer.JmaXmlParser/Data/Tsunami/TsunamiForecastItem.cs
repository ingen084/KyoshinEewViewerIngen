namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class TsunamiForecastItem<TView>
{
	/// <summary>
	/// 津波予報区
	/// </summary>
	[JmaXml("Area!")]
	public partial Area<TView> Area { get; }

	/// <summary>
	/// 津波警報等の種類
	/// </summary>
	[JmaXml("Category!")]
	public partial Category<TView> Category { get; }

	/// <summary>
	/// 津波の到達予想時刻（津波予報区）<br/>
	/// 津波警報・注意報解除時/津波予報発表時には出現しない
	/// </summary>
	[JmaXml("FirstHeight?")]
	public partial FirstHeight<TView>? FirstHeight { get; }

	/// <summary>
	/// 予想される津波の高さ（津波予報区）<br/>
	/// 津波警報・注意報解除時/津波予報発表時には出現しない
	/// </summary>
	[JmaXml("MaxHeight?")]
	public partial MaxHeight<TView>? MaxHeight { get; }

	/// <summary>
	/// 潮位観測点
	/// </summary>
	[JmaXml("Station*")]
	public partial JmaXmlList<TsunamiStation<TView>> Stations { get; }
}
