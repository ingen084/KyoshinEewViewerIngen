namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class Comments<TView>
{
	/// <summary>
	/// 固定付加文(本文)
	/// </summary>
	[JmaXml("ForecastComment?/Text!")]
	public partial string? ForecastCommentText { get; }

	/// <summary>
	/// 固定付加文(コード)
	/// </summary>
	[JmaXml("ForecastComment?/Code!")]
	public partial string? ForecastCommentCode { get; }

	/// <summary>
	/// 固定付加文(本文)
	/// </summary>
	[JmaXml("WarningComment?/Text!")]
	public partial string? WarningCommentText { get; }

	/// <summary>
	/// 固定付加文(コード)
	/// </summary>
	[JmaXml("WarningComment?/Code!")]
	public partial string? WarningCommentCode { get; }

	/// <summary>
	/// 固定付加文(その他･本文)
	/// </summary>
	[JmaXml("VarComment?/Text!")]
	public partial string? VarCommentText { get; }

	/// <summary>
	/// 固定付加文(その他･コード)
	/// </summary>
	[JmaXml("VarComment?/Code!")]
	public partial string? VarCommentCode { get; }

	/// <summary>
	/// 自由付加文
	/// </summary>
	[JmaXml("FreeFormComment?")]
	public partial string? FreeFormComment { get; }
}
