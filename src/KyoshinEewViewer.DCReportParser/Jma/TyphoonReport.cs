using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class TyphoonReport : JmaDCReport
{
	/// <summary>
	/// 台風情報の基準となる時刻(Bt)
	/// </summary>
	public DateTimeOffset ReferenceTime { get; }

	/// <summary>
	/// 基準となる時刻の種類(Dt)
	/// </summary>
	public ReferenceTimeType ReferenceTimeType { get; }

	/// <summary>
	/// 基準時刻からの経過時間(hour)(Du)
	/// </summary>
	public byte ElapsedTime { get; }

	/// <summary>
	/// 台風の番号(Tn)
	/// </summary>
	public byte TyphoonNumber { get; }

	/// <summary>
	/// 大きさのカテゴリ(Sr)
	/// </summary>
	public byte ScaleCategory { get; }

	/// <summary>
	/// 強さのカテゴリ(Ic)
	/// </summary>
	public byte IntensityCategory { get; }

	/// <summary>
	/// 緯度符号(LatNs)<br/>
	/// 0: North 1: South
	/// </summary>
	public bool LatNs { get; }
	public byte LatD { get; }
	public byte LatM { get; }
	public byte LatS { get; }
	/// <summary>
	/// 10進数の緯度
	/// </summary>
	public float Latitude => (LatD + LatM / 60f + LatS / 3600f) * (LatNs ? -1 : 1);

	/// <summary>
	/// 経度符号(LonNs)<br/>
	/// 0: East 1: West
	/// </summary>
	public bool LonEw { get; }
	public byte LonD { get; }
	public byte LonM { get; }
	public byte LonS { get; }
	/// <summary>
	/// 10進数の経度
	/// </summary>
	public float Longitude => (LonD + LonM / 60f + LonS / 3600f) * (LonEw ? -1 : 1);

	/// <summary>
	/// 中心付近の気圧(Pr)<br/>
	/// 単位は hPa
	/// </summary>
	public int CentralPressure { get; }

	/// <summary>
	/// 最大風速(W1)<br/>
	/// 不明の場合は 0
	/// </summary>
	public byte MaximumWindSpeed { get; }

	/// <summary>
	/// 瞬間最大風速(W2)<br/>
	/// 不明の場合は 0
	/// </summary>
	public byte MaximumWindGustSpeed { get; }

	public TyphoonReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		var btD1 = GetValue(53, 5);
		if (btD1 is < 1 or > 31)
			throw new DCReportParseException("BtD1 が範囲外です: " + btD1);
		var btH1 = GetValue(58, 5);
		if (btH1 is < 0 or > 23)
			throw new DCReportParseException("BtH1 が範囲外です: " + btH1);
		var btM1 = GetValue(63, 6);
		if (btM1 is < 0 or > 59)
			throw new DCReportParseException("BtM1 が範囲外です: " + btM1);
		ReferenceTime = new DateTimeOffset(4, 1, (int)btD1, (int)btH1, (int)btM1, 0, TimeSpan.Zero);

		ReferenceTimeType = (ReferenceTimeType)GetValue(69, 3);
		if (!Enum.IsDefined(ReferenceTimeType))
			throw new DCReportParseException("Dt が範囲外です: " + (int)ReferenceTimeType);

		ElapsedTime = (byte)GetValue(80, 7);

		TyphoonNumber = (byte)GetValue(87, 7);
		if (TyphoonNumber is < 1 or > 99)
			throw new DCReportParseException("Tn が範囲外です: " + TyphoonNumber);

		ScaleCategory = (byte)GetValue(94, 4);
		IntensityCategory = (byte)GetValue(98, 4);

		LatNs = GetValue(102, 1) == 1;
		LatD = (byte)GetValue(103, 7);
		if (LatD is < 0 or > 89)
			throw new DCReportParseException("LatD が範囲外です: " + LatD);
		LatM = (byte)GetValue(110, 6);
		if (LatM is < 0 or > 59)
			throw new DCReportParseException("LatM が範囲外です: " + LatM);
		LatS = (byte)GetValue(116, 6);
		if (LatS is < 0 or > 59)
			throw new DCReportParseException("LatS が範囲外です: " + LatS);

		LonEw = GetValue(142, 1) == 1;
		LonD = (byte)GetValue(123, 8);
		if (LonD is < 0 or > 179)
			throw new DCReportParseException("LonD が範囲外です: " + LonD);
		LonM = (byte)GetValue(131, 6);
		if (LonM is < 0 or > 59)
			throw new DCReportParseException("LonM が範囲外です: " + LonM);
		LonS = (byte)GetValue(137, 6);
		if (LonS is < 0 or > 59)
			throw new DCReportParseException("LonS が範囲外です: " + LonS);

		CentralPressure = (byte)GetValue(143, 11);
		if (CentralPressure is < 0 or > 1100)
			throw new DCReportParseException("Pr が範囲外です: " + CentralPressure);

		MaximumWindSpeed = (byte)GetValue(154, 7);
		if (MaximumWindSpeed is not 0 and (< 15 or > 105))
			throw new DCReportParseException("W1 が範囲外です: " + MaximumWindSpeed);

		MaximumWindGustSpeed = (byte)GetValue(161, 7);
		if (MaximumWindGustSpeed is not 0 and (< 15 or > 105))
			throw new DCReportParseException("W2 が範囲外です: " + MaximumWindGustSpeed);
	}
}

public enum ReferenceTimeType
{
	/// <summary>
	/// 実況
	/// </summary>
	Analysis = 1,
	/// <summary>
	/// 推定
	/// </summary>
	Estimate = 2,
	/// <summary>
	/// 予報
	/// </summary>
	Forecast = 3,
}
