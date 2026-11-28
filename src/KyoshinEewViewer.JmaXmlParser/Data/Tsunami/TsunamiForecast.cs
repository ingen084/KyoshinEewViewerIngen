namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class TsunamiForecast<TView>
{
	/// <summary>
	/// コード体系の定義
	/// </summary>
	[JmaXml("CodeDefine?/Type*")]
	public partial JmaXmlList<CodeDefineType<TView>> CodeDefineTypes { get; }

	/// <summary>
	/// 津波予報のアイテム
	/// </summary>
	[JmaXml("Item*")]
	public partial JmaXmlList<TsunamiForecastItem<TView>> Items { get; }
}
