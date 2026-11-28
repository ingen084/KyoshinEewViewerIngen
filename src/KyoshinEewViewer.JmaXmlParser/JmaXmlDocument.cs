using KyoshinEewViewer.JmaXmlParser.Data;
using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

namespace KyoshinEewViewer.JmaXmlParser;

/// <summary>
/// 気象庁防災情報XMLの電文。<see cref="JmaXml.Read{TView}(System.IO.Stream)"/> で読み取る
/// </summary>
/// <typeparam name="TView">読み取りの単位</typeparam>
[JmaXmlType, JmaXmlRoot("Report")]
public sealed partial class JmaXmlDocument<TView>
{
	/// <summary>
	/// 電文の管理部を取得する
	/// </summary>
	/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合</exception>
	[JmaXml("Control!")]
	public partial ControlMeta<TView> Control { get; }

	/// <summary>
	/// 電文のヘッダ部を取得する
	/// </summary>
	/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合</exception>
	[JmaXml("Head!")]
	public partial HeadData<TView> Head { get; }

	/// <summary>
	/// 地震情報の電文の内容部を取得する
	/// </summary>
	/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合</exception>
	[JmaXml("Body!")]
	public partial EarthquakeBody<TView> EarthquakeBody { get; }

	/// <summary>
	/// 津波情報の電文の内容部を取得する
	/// </summary>
	/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合</exception>
	[JmaXml("Body!")]
	public partial TsunamiBody<TView> TsunamiBody { get; }

	/// <summary>
	/// 気象情報の電文の内容部を取得する
	/// </summary>
	/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合</exception>
	[JmaXml("Body!")]
	public partial MeteorologicalBody<TView> MeteorologicalBody { get; }
}
