using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class VolcanoReport : JmaDCReport
{
	/// <summary>
	/// 時刻の曖昧さ(Du)
	/// </summary>
	public byte Ambiguity { get; }

	/// <summary>
	/// 火山活動のあった日付(Td)<br/>
	/// 不明な場合は <see cref="default"/>
	/// </summary>
	public DateTimeOffset ActivityTime { get; }

	/// <summary>
	/// 警報コード(Dw)
	/// </summary>
	public byte WarningCode { get; }

	/// <summary>
	/// 火山コード(Vo)
	/// </summary>
	public int VolcanoName { get; }

	/// <summary>
	/// Regions
	/// </summary>
	public int[] Regions { get; }

	public VolcanoReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		Ambiguity = (byte)GetValue(50, 3);

		var tdD1 = GetValue(53, 5);
		if (tdD1 is < 0 or > 31)
			throw new DCReportParseException("TdD1 が範囲外です: " + tdD1);
		var tdH1 = GetValue(58, 5);
		if (tdH1 is not 31 and (< 0 or > 23))
			throw new DCReportParseException("TdH1 が範囲外です: " + tdH1);
		var tdM1 = GetValue(63, 6);
		if (tdM1 is not 63 and (< 0 or > 59))
			throw new DCReportParseException("TdM1 が範囲外です: " + tdM1);
		if (tdD1 == 0 && tdH1 == 31 && tdM1 == 63)
			ActivityTime = default;
		else
			ActivityTime = new DateTimeOffset(4, 1, (int)tdD1, (int)tdM1, (int)tdM1, 0, TimeSpan.Zero);

		WarningCode = (byte)GetValue(69, 7);
		if (WarningCode == 0)
			throw new DCReportParseException("Dw が範囲外です: " + WarningCode);

		VolcanoName = (int)GetValue(76, 12);
		if (VolcanoName is < 101 or > 4000)
			throw new DCReportParseException("Vo が範囲外です: " + VolcanoName);

		Regions = new int[5];
		for (var i = 0; i < 5; i++)
		{
			Regions[i] = (int)GetValue(88 + i * 23, 23);
			if (i == 0 && Regions[i] == 0 || Regions[i] is < 110000 or > 4799999)
				throw new DCReportParseException($"Pl_{i} が範囲外です: " + Regions[i]);
		}
	}
}
