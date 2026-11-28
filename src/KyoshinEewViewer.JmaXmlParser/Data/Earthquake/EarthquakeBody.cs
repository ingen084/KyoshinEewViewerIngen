namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震情報における内容部
/// </summary>
[JmaXmlType]
public sealed partial class EarthquakeBody<TView>
{
	/// <summary>
	/// 命名地震<br/>
	/// 顕著な被害を起こした地震について命名した場合、記載される
	/// </summary>
	[JmaXml("Naming?")]
	public partial string? Naming { get; }

	/// <summary>
	/// 命名地震(英語)<br/>
	/// 顕著な被害を起こした地震について命名した場合、記載される
	/// </summary>
	[JmaXml("Naming?@english?")]
	public partial string? ForecastCommentText { get; }

	/// <summary>
	/// 地震回数
	/// </summary>
	[JmaXml("EarthquakeCount?")]
	public partial EarthquakeCount<TView>? EarthquakeCount { get; }

	/// <summary>
	/// 地震の諸要素（発生日時、震央地名、震源要素、マグニチュード等）
	/// </summary>
	[JmaXml("Earthquake?")]
	public partial EarthquakeData<TView>? Earthquake { get; }

	/// <summary>
	/// 震度・長周期地震動階級に関する情報
	/// </summary>
	[JmaXml("Intensity?")]
	public partial Intensity<TView>? Intensity { get; }

	/// <summary>
	/// 次回発表予定<br/>
	/// 続報を発表する予定がある場合は、次回発表予定時刻に関する情報<br/>
	/// EEWでは最終報の場合のみ
	/// </summary>
	[JmaXml("NextAdvisory?")]
	public partial string? NextAdvisory { get; }

	/// <summary>
	/// コメント
	/// </summary>
	[JmaXml("Comments?")]
	public partial Comments<TView>? Comments { get; }

	/// <summary>
	/// 自由文形式で追加的に情報を記載する必要がある場合等
	/// </summary>
	[JmaXml("Text?")]
	public partial string? Text { get; }
}
