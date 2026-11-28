namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class TsunamiObservation<TView>
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
	public partial JmaXmlList<TsunamiObservationItem<TView>> Items { get; }
}
