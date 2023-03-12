using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class AshFallReport : JmaDCReport
{
	/// <summary>
	/// 火山活動のあった日付(Td)<br/>
	/// 不明な場合は <see cref="default"/>
	/// </summary>
	public DateTimeOffset ActivityTime { get; }

	/// <summary>
	/// 警報の種類(Dw1)
	/// </summary>
	public byte WarningType { get; }

	/// <summary>
	/// 火山名(Vo)
	/// </summary>
	public int VolcanoNameCode { get; }

	/// <summary>
	/// 各地域の降灰予想時間(Ho/Dw2/Pl)
	/// </summary>
	public (byte ExpectedTime, byte WarningCode, int Region)[] Regions { get; }

	public AshFallReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		var tdD1 = GetValue(53, 5);
		if (tdD1 is < 0 or > 31)
			throw new DCReportParseException("TdD1 が範囲外です: " + tdD1);
		var tdH1 = GetValue(58, 5);
		if (tdH1 is < 0 or > 23)
			throw new DCReportParseException("TdH1 が範囲外です: " + tdH1);
		var tdM1 = GetValue(63, 6);
		if (tdM1 is < 0 or > 59)
			throw new DCReportParseException("TdM1 が範囲外です: " + tdM1);
		ActivityTime = new DateTimeOffset(4, 1, (int)tdD1, (int)tdH1, (int)tdM1, 0, TimeSpan.Zero);

		WarningType = (byte)GetValue(69, 2);
		if (WarningType is < 1 or > 2)
			throw new DCReportParseException("Dw1 が範囲外です: " + WarningType);

		VolcanoNameCode = (int)GetValue(71, 12);
		if (VolcanoNameCode is < 101 or > 4000)
			throw new DCReportParseException("Vo が範囲外です: " + VolcanoNameCode);

		Regions = new (byte ExpectedTime, byte WarningCode, int Region)[4];
		for (var i = 0; i < 4; i++)
		{
			var offset = 83 + i * 29;
			var ho = (byte)GetValue(offset, 3);
			if (i == 0 && ho == 0 || ho is < 0 or > 6)
				throw new DCReportParseException($"Ho_{i + 1} が範囲外です: " + ho);
			var dw2 = (byte)GetValue(offset + 3, 3);
			if (i == 0 && dw2 == 0 || dw2 is < 0 or > 7)
				throw new DCReportParseException($"Dw2_{i + 1} が範囲外です: " + dw2);
			var pl = GetValue(offset + 6, 23);
			if (i == 0 && pl == 0 || pl is < 110000 or > 4799999)
				throw new DCReportParseException($"Pl_{i + 1} が範囲外です: " + pl);
			Regions[i] = (ho, dw2, (int)pl);
		}
	}
}
