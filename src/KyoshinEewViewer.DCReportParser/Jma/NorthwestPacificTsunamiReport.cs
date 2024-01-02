using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class NorthwestPacificTsunamiReport : JmaDCReport
{
	/// <summary>
	/// 津波の可能性(Tp)<br/>
	/// </summary>
	public byte TsunamigenicPotential { get; }

	/// <summary>
	/// 予想される津波の到達時刻･高さ(Ta/Th/Pl)<br/>
	/// ArrivalTime の日付は 西暦4年1月1日 となる
	/// </summary>
	public (bool IsArrived, DateTimeOffset ArrivalTime, int Height, byte Region)[] Regions { get; }

	public NorthwestPacificTsunamiReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		TsunamigenicPotential = (byte)GetValue(53, 3);

		Regions = new (bool IsArrived, DateTimeOffset ArrivalTime, int Height, byte Region)[5];
		for (var i = 0; i < 5; i++)
		{
			var offset = 56 + i * 28;
			var taD1 = GetValue(offset, 1) == 1;
			var taH1 = (byte)GetValue(offset + 1, 5);
			if (taH1 is not 31 and (< 0 or > 23))
				throw new DCReportParseException($"TaH1_{i + 1} が範囲外です: " + taH1);
			var taM1 = (byte)GetValue(offset + 6, 6);
			if (taM1 is not 63 and (< 0 or > 59))
				throw new DCReportParseException($"TaM1_{i + 1} が範囲外です: " + taM1);

			var isArrived = taH1 == 31 && taM1 == 63;
			var arrivalTime = DateTimeOffset.MinValue;
			if (!isArrived)
			{
				arrivalTime = new DateTimeOffset(4, 1, 1, taH1, taM1, 0, TimeSpan.Zero);
				if (taD1)
					arrivalTime = arrivalTime.AddDays(1);
			}

			var th = GetValue(offset + 12, 9);
			if ((i == 0 && th == 0) || th is (< 0 or > 4) and (< 508 or > 511))
				throw new DCReportParseException($"Th_{i + 1} が範囲外です: " + th);

			var pl = (byte)GetValue(offset + 21, 7);
			if ((i == 0 && pl == 0) || pl is < 0 or > 100)
				throw new DCReportParseException($"Pl_{i + 1} が範囲外です: " + pl);

			Regions[i] = (isArrived, arrivalTime, (int)th, pl);
		}
	}
}
