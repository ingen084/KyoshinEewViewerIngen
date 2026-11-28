using System;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class TsunamiStation<TView>
{
	/// <summary>
	/// 観測点名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 地域コード
	/// </summary>
	[JmaXml("Code!")]
	public partial int Code { get; }

	/// <summary>
	/// 満潮時刻(Forecast のみ)
	/// </summary>
	[JmaXml("HighTideDateTime?")]
	public partial DateTimeOffset? HighTideDateTime { get; }

	/// <summary>
	/// Observation: 観測した津波の第１波<br/>
	/// Forecast: 津波の到達予想時刻
	/// </summary>
	[JmaXml("FirstHeight?")]
	public partial FirstHeight<TView>? FirstHeight { get; }

	/// <summary>
	/// 観測したこれまでの最大波(Observation のみ)
	/// </summary>
	[JmaXml("MaxHeight?")]
	public partial MaxHeight<TView>? MaxHeight { get; }
}
