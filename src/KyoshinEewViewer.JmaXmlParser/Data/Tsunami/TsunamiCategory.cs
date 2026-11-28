namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

/// <summary>
/// 津波警報等の種類
/// </summary>
[JmaXmlType]
public sealed partial class TsunamiCategory<TView>
{
	/// <summary>
	/// 今回の津波警報等の種類
	/// </summary>
	[JmaXml("Kind!")]
	public partial Kind<TView> Kind { get; }

	/// <summary>
	/// 前回の津波警報等の種類
	/// </summary>
	[JmaXml("LastKind!")]
	public partial Kind<TView> LastKind { get; }
}
