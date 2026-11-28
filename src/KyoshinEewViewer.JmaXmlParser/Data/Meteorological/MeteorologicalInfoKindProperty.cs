using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct MeteorologicalInfoKindProperty(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _type = null;
	/// <summary>
	/// 要素名
	/// </summary>
	public string Type => _type ??= (Node.TryFindStringNode(Literals.Type(), out var n) ? n : throw new JmaXmlParseException("Type ノードが存在しません"));

	/// <summary>
	/// 危険度・量的予想の構成部品を記載順で取得する。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> ForecastParts => Node.Children
		.Where(c => c.Name == "SignificancyPart"u8 || c.Name == "SubsequentSignificancyPart"u8 ||
			c.Name == "CriteriaPeriod"u8 || c.Name == "WindDirectionPart"u8 || c.Name == "WindSpeedPart"u8 ||
			c.Name == "PrecipitationPart"u8 || c.Name == "PrecipitationBasedIndexPart"u8 ||
			c.Name == "WaveHeightPart"u8 || c.Name == "TidalLevelPart"u8 || c.Name == "SnowfallDepthPart"u8 ||
			c.Name == "SnowDepthPart"u8 || c.Name == "HumidityPart"u8 || c.Name == "VisibilityPart"u8 || c.Name == "TemperaturePart"u8)
		.Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 気象現象の危険度。地域別・時刻別の分類は Base 以下に保持する。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> SignificancyParts
		=> Node.Children.Where(c => c.Name == "SignificancyPart"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 予報対象期間より後の危険度。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> SubsequentSignificancyParts
		=> Node.Children.Where(c => c.Name == "SubsequentSignificancyPart"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 警戒期間。存在しない場合は null。
	/// </summary>
	public MeteorologicalPeriod? WarningPeriod => Node.TryFindChild("WarningPeriod"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 注意期間。存在しない場合は null。
	/// </summary>
	public MeteorologicalPeriod? AdvisoryPeriod => Node.TryFindChild("AdvisoryPeriod"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 危険度の基準に達する時刻・期間。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? CriteriaPeriod => Node.TryFindChild("CriteriaPeriod"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 現象のピークの時間帯。存在しない場合は null。
	/// </summary>
	public ForecastTerm? PeakTime => Node.TryFindChild("PeakTime"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 風向の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? WindDirectionPart => Node.TryFindChild("WindDirectionPart"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 風速の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? WindSpeedPart => Node.TryFindChild("WindSpeedPart"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 波高の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? WaveHeightPart => Node.TryFindChild("WaveHeightPart"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 潮位の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? TidalLevelPart => Node.TryFindChild("TidalLevelPart"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 雨量の予想。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> PrecipitationParts
		=> Node.Children.Where(c => c.Name == "PrecipitationPart"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 雨量に基づく指数の予想。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> PrecipitationBasedIndexParts
		=> Node.Children.Where(c => c.Name == "PrecipitationBasedIndexPart"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 降雪量の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? SnowfallDepthPart => Node.TryFindChild("SnowfallDepthPart"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 積雪深の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? SnowDepthPart => Node.TryFindChild("SnowDepthPart"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 湿度の予想。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> HumidityParts
		=> Node.Children.Where(c => c.Name == "HumidityPart"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 視程の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? VisibilityPart => Node.TryFindChild("VisibilityPart"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 気温の予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? TemperaturePart => Node.TryFindChild("TemperaturePart"u8, out var n) ? new(n) : null;

	private TyphoonNamePart? _typhoonNamePart = null;
	/// <summary>
	/// Type=呼称 のときに出現
	/// </summary>
	public TyphoonNamePart TyphoonNamePart => _typhoonNamePart ??= (Node.TryFindChild(Literals.TyphoonNamePart(), out var n) ? new(n) : throw new JmaXmlParseException("TyphoonNamePart ノードが存在しません"));

	private ClassPart? _classPart = null;
	/// <summary>
	/// Type=階級 のときに出現
	/// </summary>
	public ClassPart ClassPart => _classPart ??= (Node.TryFindChild(Literals.ClassPart(), out var n) ? new(n) : throw new JmaXmlParseException("ClassPart ノードが存在しません"));

	private CenterPart? _centerPart = null;
	/// <summary>
	/// Type=中心 のときに出現
	/// </summary>
	public CenterPart CenterPart => _centerPart ??= (Node.TryFindChild(Literals.CenterPart(), out var n) ? new(n) : throw new JmaXmlParseException("CenterPart ノードが存在しません"));

	private WindPart? _windPart = null;
	/// <summary>
	/// Type=風 のときに出現
	/// </summary>
	public WindPart WindPart => _windPart ??= (Node.TryFindChild(Literals.WindPart(), out var n) ? new(n) : throw new JmaXmlParseException("WindPart ノードが存在しません"));

	/// <summary>
	/// Type=風 のときに出現
	/// </summary>
	public IEnumerable<WarningAreaPart> WarningAreaParts
		=> Node.Children.Where(c => c.Name == Literals.WarningAreaPart()).Select(c => new WarningAreaPart(c));
}
