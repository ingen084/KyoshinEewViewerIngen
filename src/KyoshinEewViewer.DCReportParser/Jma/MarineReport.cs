using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class MarineReport : JmaDCReport
{
	/// <summary>
	/// 海上警報･注意報の地域(Dw,Pl)
	/// </summary>
	public (byte WarningCode, int Region)[] Regions { get; }

	public MarineReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		Regions = new (byte WarningCode, int Region)[8];
		for (var i = 0; i < 8; i++)
		{
			var offset = 53 + i * 19;

			var ww = (byte)GetValue(offset, 5);
			if (i == 0 && ww == 0)
				throw new DCReportParseException($"Dw_{i + 1} が範囲外です: " + ww);

			var pl = GetValue(offset + 5, 14);
			if (i == 0 && pl == 0 || pl is not 0 and (< 1000 or > 10000))
				throw new DCReportParseException($"Pl_{i + 1} が範囲外です: " + ww);

			Regions[i] = (ww, (int)pl);
		}
	}
}
