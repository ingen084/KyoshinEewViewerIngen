using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class TsunamiBody<TView>
{
	/// <summary>
	/// 津波に関連する情報<br/>
	/// InfoType が "取消" の場合、 null
	/// </summary>
	[JmaXml("Tsunami?")]
	public partial TsunamiData<TView>? Tsunami { get; }

	/// <summary>
	/// 地震の諸要素<br/>
	/// InfoType が "取消" の場合、 null<br/>
	/// 複数の地震が原因の場合は地震ごとに項目が存在する
	/// </summary>
	[JmaXml("Earthquake*")]
	public partial JmaXmlList<EarthquakeData<TView>> Earthquakes { get; }

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
