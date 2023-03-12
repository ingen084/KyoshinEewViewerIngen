using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class SeismicIntensityReport : JmaDCReport
{
	/// <summary>
	/// 地震の発生時刻(Ot)<br/>
	/// 情報が存在しない年月については 西暦4年1月1日 になるので注意
	/// </summary>
	public DateTimeOffset OccurrenceTime { get; }

	/// <summary>
	/// 観測震度+都道府県<br/>
	/// 空データは Es = None, Pl = 0 になる
	/// </summary>
	public (SeismicIntensity Es, byte Pl)[] Regions { get; }

	public SeismicIntensityReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		var otD1 = (byte)GetValue(53, 5);
		if (otD1 is < 1 or > 31)
			throw new DCReportParseException("OtD1 が範囲外です: " + otD1);
		var otH1 = (byte)GetValue(58, 5);
		if (otH1 is < 0 or > 23)
			throw new DCReportParseException("OtH1 が範囲外です: " + otH1);
		var otM1 = (byte)GetValue(63, 6);
		if (otM1 is < 0 or > 59)
			throw new DCReportParseException("OtM1 が範囲外です: " + otM1);
		OccurrenceTime = new DateTimeOffset(4, 1, otD1, otH1, otM1, 0, TimeSpan.Zero);

		Regions = new (SeismicIntensity Es, byte Pl)[16];
		for (var i = 0; i < 16; i++)
		{
			var offset = 69 + i * 9;
			var es = (SeismicIntensity)GetValue(offset, 3);
			if (i == 0 && es == SeismicIntensity.None || !Enum.IsDefined(es))
				throw new DCReportParseException($"Es_{i + 1} が範囲外です: " + (int)es);
			var pl = (byte)GetValue(offset + 3, 6);
			if (i == 0 && pl == 0 || pl is < 0 or > 47)
				throw new DCReportParseException($"Pl_{i + 1} が範囲外です: " + pl);
			Regions[i] = (es, pl);
		}
	}
}

public enum SeismicIntensity
{
	None = 0,
	/// <summary>
	/// 震度4未満<br/>
	/// 震度4以上だったが更新報で震度4未満に更新された場合
	/// </summary>
	LessThanInt4 = 1,
	Int4 = 2,
	Int5Lower = 3,
	Int5Upper = 4,
	Int6Lower = 5,
	Int6Upper = 6,
	Int7 = 7,
}
