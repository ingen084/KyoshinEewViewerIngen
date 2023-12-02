using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class TsunamiReport : JmaDCReport
{
	/// <summary>
	/// 防災に関するお知らせ(Co_1 .. Co_3)
	/// </summary>
	public int[] Information { get; }

	/// <summary>
	/// 警報コード(Dw)<br/>
	/// 電文の改定により未定義の値を送信することがある
	/// </summary>
	public byte WarningCode { get; }

	/// <summary>
	/// 予想される津波の到達時刻･高さ(Ta/Th/Pl)<br/>
	/// ArrivalTime は 西暦4年1月1日 がベースになる
	/// </summary>
	public (bool IsArrived, DateTimeOffset ArrivalTime, byte Height, int Region)[] Regions { get; }

	public TsunamiReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		static int CheckCoRange(long value, int index)
		{
			if (value is not 0 and (< 101 or > 500))
				throw new DCReportParseException($"Co_{index} が範囲範囲外です: " + value);
			return (int)value;
		}
		Information = [
			CheckCoRange(GetValue(53, 9), 1),
			CheckCoRange(GetValue(62, 9), 2),
			CheckCoRange(GetValue(71, 9), 3),
		];

		WarningCode = (byte)GetValue(80, 4);

		Regions = new (bool IsArrived, DateTimeOffset ArrivalTime, byte Height, int Region)[5];
		for (var i = 0; i < 5; i++)
		{
			var offset = 84 + i * 26;
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

			var th = (byte)GetValue(offset + 12, 4);
			if (i == 0 && th == 0 || th is < 0 or > 15)
				throw new DCReportParseException($"Th_{i + 1} が範囲外です: " + th);

			var pl = GetValue(offset + 16, 10);
			if (i == 0 && pl == 0 || pl is not 0 and (< 100 or > 1000))
				throw new DCReportParseException($"Pl_{i + 1} が範囲外です: " + pl);

			Regions[i] = (isArrived, arrivalTime, th, (int)pl);
		}
	}
}
