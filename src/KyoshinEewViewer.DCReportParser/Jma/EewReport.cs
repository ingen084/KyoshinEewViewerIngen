using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class EewReport : JmaDCReport
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
	/// 不明の場合 511<br/>
	/// マグニチュードが 10 の場合 10 になる
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
	/// <see cref="Magnitude"/> に 0.1 を掛けたもの
	/// </summary>
	public float MagnitudeFp => Magnitude * 0.1f;

	/// <summary>
	/// 震央(Ep)
	/// </summary>
	public int Epicenter { get; }

	/// <summary>
	/// 最低震度(Ll)
	/// </summary>
	public EewSeismicIntensity SeismicIntensityLowerLimit { get; }

	/// <summary>
	/// 最大震度(Ul)
	/// </summary>
	public EewSeismicIntensity SeismicIntensityUpperLimit { get; }

	/// <summary>
	/// 各地域の警報状況(Pl_1 .. Pl_80)
	/// </summary>
	public bool[] WarningRegions { get; }

	public EewReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
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
		if (Magnitude is not 127 and (< 0 or > 101))
			throw new DCReportParseException("Ma が範囲外です: " + Magnitude);

		Epicenter = (int)GetValue(112, 10);
		if (Epicenter is not 0 and < 11 or > 1000)
			throw new DCReportParseException("Ep が範囲外です: " + Epicenter);

		var ll = (EewSeismicIntensity)GetValue(122, 4);
		if (!Enum.IsDefined(ll) || ll == EewSeismicIntensity.Over)
			throw new DCReportParseException("Ll が範囲外です: " + (int)ll);
		SeismicIntensityLowerLimit = ll;

		var ul = (EewSeismicIntensity)GetValue(126, 4);
		if (!Enum.IsDefined(ul))
			throw new DCReportParseException("Ul が範囲外です: " + (int)ul);
		SeismicIntensityUpperLimit = ul;

		WarningRegions = new bool[80];
		for (var i = 0; i < 80; i++)
			WarningRegions[i] = GetValue(130 + i, 1) == 1;
	}
}

public enum EewSeismicIntensity
{
	Int0 = 1,
	Int1 = 2,
	Int2 = 3,
	Int3 = 4,
	Int4 = 5,
	Int5Lower = 6,
	Int5Upper = 7,
	Int6Lower = 8,
	Int6Upper = 9,
	Int7 = 10,
	Over = 11,
	None = 14,
	Unknown = 15,
}
