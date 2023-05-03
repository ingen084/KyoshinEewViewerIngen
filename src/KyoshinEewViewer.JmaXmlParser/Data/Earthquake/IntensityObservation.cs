using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct IntensityObservation
{
	private XmlNode Node { get; set; }

	public IntensityObservation(XmlNode node)
	{
		Node = node;
	}

	/// <summary>
	/// コード体系の定義
	/// </summary>
	public IEnumerable<CodeDefineType> CodeDefineTypes
	{
		get {
			if (!Node.TryFindChild(Literals.CodeDefine(), out var n))
				return Enumerable.Empty<CodeDefineType>();
			return n.Children.Where(c => c.Name == Literals.Type()).Select(c => new CodeDefineType(c));
		}
	}

	private string? _maxInt = null;
	/// <summary>
	/// 最大震度
	/// </summary>
	public string? MaxInt => _maxInt ??= (Node.TryFindStringNode(Literals.MaxInt(), out var n) ? n : null);

	private string? _maxLgInt = null;
	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	public string? MaxLgInt => _maxLgInt ??= (Node.TryFindStringNode(Literals.MaxLgInt(), out var n) ? n : null);

	private string? _lgCategory = null;
	/// <summary>
	/// 長周期地震動に関する観測情報の種類<br/>
	/// 1. 全国の最大長周期地震動階級が２以下で、長周期地震動階級１以上が観測されたすべての地域において最大震度が５弱以上である<br/>
	/// 2. 全国の最大長周期地震動階級が２以下で、長周期地震動階級１以上が観測された地域のうち最大震度が４以下となる地域が存在している<br/>
	/// 3. 全国の最大長周期地震動階級が３以上で、長周期地震動階級３以上が観測されたすべての地域において最大震度が５弱以上である<br/>
	/// 4. 全国の最大長周期地震動階級が３以上で、長周期地震動階級３以上が観測された地域のうち最大震度が４以下となる地域が存在している
	/// </summary>
	public string? LgCategory => _lgCategory ??= (Node.TryFindStringNode(Literals.LgCategory(), out var n) ? n : null);

	/// <summary>
	/// 都道府県毎の震度の観測状況
	/// </summary>
	public IEnumerable<Pref> Prefs
		=> Node.Children.Where(c => c.Name == Literals.Pref()).Select(c => new Pref(c));
}
