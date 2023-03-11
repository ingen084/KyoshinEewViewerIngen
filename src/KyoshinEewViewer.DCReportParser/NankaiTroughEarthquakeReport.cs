using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser;

public class NankaiTroughEarthquakeReport : JmaDCReport
{
	/// <summary>
	/// シリアルコード(Is)
	/// </summary>
	public InformationSerialCode InformationSerialCode { get; }

	/// <summary>
	/// テキスト情報(Te_1 .. Te_18)<br/>
	/// UTF-8
	/// </summary>
	public byte[] TextInformation { get; }

	/// <summary>
	/// ページ番号(Pn)
	/// </summary>
	public byte PageNumber { get; }

	/// <summary>
	/// ページ総数(Pm)
	/// </summary>
	public byte TotalPage { get; }

	public NankaiTroughEarthquakeReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		InformationSerialCode = (InformationSerialCode)GetValue(53, 4);
		if (!Enum.IsDefined(InformationSerialCode))
			throw new DCReportParseException("Is が範囲外です: " + (byte)InformationSerialCode);

		TextInformation = new byte[18];
		for (var i = 0; i < 18; i++)
			TextInformation[i] = (byte)GetValue(57 + i * 8, 8);

		PageNumber = (byte)GetValue(201, 6);
		if (PageNumber == 0)
			throw new DCReportParseException("Pn が範囲外です: " + PageNumber);
		TotalPage = (byte)GetValue(207, 6);
		if (TotalPage == 0)
			throw new DCReportParseException("Pm が範囲外です: " + TotalPage);
	}
}

public enum InformationSerialCode
{
	/// <summary>
	/// 調査中A<br/>
	/// 監視領域内でマグニチュード 6.8 以上の地震が発生したことにより、臨時に「南海トラフ沿いの地震に関する評価検討会」を開催する場合。
	/// </summary>
	InvestigatingA = 1,
	/// <summary>
	/// 調査中B<br/>
	/// 1 カ所以上のひずみ計での有意な変化と共に、他の複数の観測点でもそれに関係すると思われる変化が観測され、想定震源域内のプレート境界で通常と異なるゆっくりすべりが発生している可能性がある場合など、ひずみ計で南海トラフ地震との関連性の検討が必要と認められる変化を観測したことにより、臨時に「南海トラフ沿いの地震に関する評価検討会」を開催する場合。
	/// </summary>
	InvestigatingB = 2,
	/// <summary>
	/// 調査中C<br/>
	/// その他、想定震源域内のプレート境界の固着状態の変化を示す可能性のある現象が観測される等、南海トラフ地震との関連性の検討が必要と認められる現象を観測したことにより、臨時に「南海トラフ沿いの地震に関する評価検討会」を開催する場合。
	/// </summary>
	InvestigatingC = 3,
	/// <summary>
	/// 巨大地震警戒
	/// </summary>
	HugeEarthquakeWarning = 4,
	/// <summary>
	/// 巨大地震注意
	/// </summary>
	HugeEarthquakeCaution = 5,
	/// <summary>
	/// 調査終了
	/// </summary>
	InvestigateEnded = 6,
	Other = 15,
}
