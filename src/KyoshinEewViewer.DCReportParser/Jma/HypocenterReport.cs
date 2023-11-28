using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;
public class HypocenterReport : JmaDCReport
{
	/// <summary>
	/// 防災に関するお知らせ(Co_1 .. Co_3)
	/// </summary>
	public int[] Information { get; }

	/// <summary>
	/// 地震の発生時刻(Ot)<br/>
	/// 情報が存在しない年月については 1月1日 になるので注意
	/// </summary>
	public DateTimeOffset OccurrenceTime { get; }

	/// <summary>
	/// 震源の深さ(De)<br/>
	/// 500km 以上の場合 501<br/>
	/// 不明の場合 511
	/// </summary>
	public int Depth { get; }

	/// <summary>
	/// 0.1 単位の規模(Ma)<br/>
	/// 10.0 以上の場合 101<br/>
	/// 不明の場合 127<br/>
	/// 仮定震源要素の場合 10
	/// </summary>
	public byte Magnitude { get; }

	/// <summary>
	/// 規模<br/>
	/// <see cref="Magnitude"/> に 0.1 を掛けたもの<br/>
	/// 不明 の場合は null
	/// </summary>
	public float? MagnitudeFp => Magnitude == 127 ? null : Magnitude * 0.1f;

	/// <summary>
	/// 震央(Ep)
	/// </summary>
	public int Epicenter { get; }

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

	public HypocenterReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		static int CheckCoRange(long value, int index)
		{
			if (value is not 0 and (< 101 or > 500))
				throw new DCReportParseException($"Co_{index} が範囲範囲外です: " + value);
			return (int)value;
		}
		Information = new[]
		{
			CheckCoRange(GetValue(53, 9), 1),
			CheckCoRange(GetValue(62, 9), 2),
			CheckCoRange(GetValue(71, 9), 3),
		};

		var otD1 = (byte)GetValue(80, 5);
		if (otD1 is < 1 or > 31)
			throw new DCReportParseException("OtD1 が範囲外です: " + otD1);
		var otH1 = (byte)GetValue(85, 5);
		if (otH1 is < 0 or > 23)
			throw new DCReportParseException("OtH1 が範囲外です: " + otH1);
		var otM1 = (byte)GetValue(90, 6);
		if (otM1 is < 0 or > 59)
			throw new DCReportParseException("OtM1 が範囲外です: " + otM1);
		OccurrenceTime = new DateTimeOffset(4, 1, otD1, otH1, otM1, 0, TimeSpan.Zero);

		Depth = (int)GetValue(96, 9);
		if (Depth is not 511 and (< 0 or > 501))
			throw new DCReportParseException("De が範囲外です: " + Depth);

		Magnitude = (byte)GetValue(105, 7);
		if (Magnitude is not 126 and not 127 and (< 0 or > 101))
			throw new DCReportParseException("Ma が範囲外です: " + Magnitude);

		Epicenter = (int)GetValue(112, 10);
		if (Epicenter is not 0 and < 11 or > 1000)
			throw new DCReportParseException("Ep が範囲外です: " + Epicenter);

		LatNs = GetValue(122, 1) == 1;
		LatD = (byte)GetValue(123, 7);
		if (LatD is < 0 or > 89)
			throw new DCReportParseException("LatD が範囲外です: " + LatD);
		LatM = (byte)GetValue(130, 6);
		if (LatM is < 0 or > 59)
			throw new DCReportParseException("LatM が範囲外です: " + LatM);
		LatS = (byte)GetValue(136, 6);
		if (LatS is < 0 or > 59)
			throw new DCReportParseException("LatS が範囲外です: " + LatS);

		LonEw = GetValue(142, 1) == 1;
		LonD = (byte)GetValue(143, 8);
		if (LonD is < 0 or > 179)
			throw new DCReportParseException("LonD が範囲外です: " + LonD);
		LonM = (byte)GetValue(151, 6);
		if (LonM is < 0 or > 59)
			throw new DCReportParseException("LonM が範囲外です: " + LonM);
		LonS = (byte)GetValue(157, 6);
		if (LonS is < 0 or > 59)
			throw new DCReportParseException("LonS が範囲外です: " + LonS);
	}
}
