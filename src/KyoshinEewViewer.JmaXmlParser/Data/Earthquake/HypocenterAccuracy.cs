using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct HypocenterAccuracy
{
	private XmlNode Node { get; set; }

	public HypocenterAccuracy(XmlNode node)
	{
		Node = node;
	}

	private int? _epicenterRank = null;
	/// <summary>
	/// 震源位置の精度のランク <c>0~8</c>
	/// <list type="number" start="0">
	///		<listheader>不明</listheader>
	///		<item>P 波／S 波レベル超え、IPF法（1 点）、または「仮定震源要素」の場合 〔気象庁データ〕</item>
	///		<item>IPF 法（2 点） 〔気象庁データ〕</item>
	///		<item>IPF 法（3 点／4 点） 〔気象庁データ〕</item>
	///		<item>IPF 法（5 点以上）） 〔気象庁データ〕</item>
	///		<item>防災科研システム（4 点以下、または精度情報なし）<br/>
	///		〔防災科学技術研究所データ[以下、防災科研 Hi-net データ]〕</item>
	///		<item>防災科研システム（5 点以上） 〔防災科研 Hi-net データ〕</item>
	///		<item>EPOS（海域〔観測網外〕）</item>
	///		<item>EPOS（内陸〔観測網内〕）</item>
	/// </list>
	/// </summary>
	public int EpicenterRank
	{
		get {
			if (_epicenterRank is { } r)
				return r;
			if (!Node.TryFindChild(Literals.Epicenter(), out var n))
				throw new JmaXmlParseException("Epicenter ノードが存在しません");
			if (!n.TryFindIntAttribute(Literals.AttrRank(), out r))
				throw new JmaXmlParseException("rank 属性が存在しません");
			_epicenterRank = r;
			return r;
		}
	}

	private int? _epicenterRank2 = null;
	/// <summary>
	/// 震源位置の精度のランク2 <c>0~4,9</c><br/>
	/// <list type="number">
	///		<listheader>不明</listheader>
	///		<item>P 波／S 波レベル超え、IPF 法（1 点）、または「仮定震源要素」の場合</item>
	///		<item>IPF 法（2 点）</item>
	///		<item>IPF 法（3 点／4 点）</item>
	///		<item>IPF 法（5 点以上）</item>
	///	</list>
	///	9. 震源とマグニチュードに基づく震度予測手法での精度が最終報相当<br/>（推定震源とマグニチュードはこれ以降変化しない。ただし、PLUM法により予測震度が今後変化する可能性はある。）
	/// <para>（※1,9 以外については気象庁の部内システムでの利用（予告無く変更することがある））</para>
	/// </summary>
	public int EpicenterRank2
	{
		get {
			if (_epicenterRank2 is { } r)
				return r;
			if (!Node.TryFindChild(Literals.Epicenter(), out var n))
				throw new JmaXmlParseException("Epicenter ノードが存在しません");
			if (!n.TryFindIntAttribute(Literals.AttrRank2(), out r))
				throw new JmaXmlParseException("rank2 属性が存在しません");
			_epicenterRank2 = r;
			return r;
		}
	}

	private int? _depthRank = null;
	/// <summary>
	/// 震源深さの精度のランク <c>0~8</c>
	/// <list type="number" start="0">
	///		<listheader>不明</listheader>
	///		<item>P 波／S 波レベル超え、IPF 法（1 点）、または仮定震源要素の場合</item>
	///		<item>IPF 法（2 点）</item>
	///		<item>IPF 法（3 点／4 点）</item>
	///		<item>IPF 法（5 点以上）</item>
	///		<item>防災科研システム（4 点以下、または精度情報なし）<br/>〔防災科学技術研究所データ[以下、防災科研 Hi-net データ]〕</item>
	///		<item>防災科研システム（5 点以上） 〔防災科研 Hi-net データ〕</item>
	///		<item>EPOS（海域〔観測網外〕）</item>
	///		<item>EPOS（内陸〔観測網内〕）</item>
	/// </list>
	/// </summary>
	public int DepthRank
	{
		get {
			if (_depthRank is { } r)
				return r;
			if (!Node.TryFindChild(Literals.Depth(), out var n))
				throw new JmaXmlParseException("Depth ノードが存在しません");
			if (!n.TryFindIntAttribute(Literals.AttrRank(), out r))
				throw new JmaXmlParseException("rank 属性が存在しません");
			_depthRank = r;
			return r;
		}
	}

	private int? _magnitudeCalculationRank = null;
	/// <summary>
	/// マグニチュードの精度値 <c>0,2~8</c>
	/// <para>0. 不明<br/>
	/// 2. 防災科研システム 〔防災科研 Hi-net データ〕<br/>
	/// 3. 全点 P 相<br/>
	/// 4. P 相／全相混在<br/>
	/// 5. 全点全相<br/>
	/// 6. EPOS<br/>
	/// 8. P 波／S 波レベル超え、または仮定震源要素の場合</para>
	/// </summary>
	public int MagnitudeCalculationRank
	{
		get {
			if (_magnitudeCalculationRank is { } r)
				return r;
			if (!Node.TryFindChild(Literals.MagnitudeCalculation(), out var n))
				throw new JmaXmlParseException("MagnitudeCalculation ノードが存在しません");
			if (!n.TryFindIntAttribute(Literals.AttrRank(), out r))
				throw new JmaXmlParseException("rank 属性が存在しません");
			_magnitudeCalculationRank = r;
			return r;
		}
	}

	private int? _numberOfMagnitudeCalculation = null;
	/// <summary>
	/// マグニチュード計算使用観測点数 <c>0~5</c>
	/// <list type="number" start="0">
	///		<listheader>不明</listheader>
	///		<item>1 点、P 波／S 波レベル超え、または仮定震源要素の場合</item>
	///		<item>2 点</item>
	///		<item>3 点</item>
	///		<item>4 点</item>
	///		<item>5 点以上</item>
	/// </list>
	/// </summary>
	public int NumberOfMagnitudeCalculation => _numberOfMagnitudeCalculation ??= (Node.TryFindIntNode(Literals.NumberOfMagnitudeCalculation(), out var n) ? n : throw new JmaXmlParseException("Area ノードが存在しません"));
}
