using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class FloodReport : JmaDCReport
{
	/// <summary>
	/// 洪水予報地域(Lv,Pl)
	/// </summary>
	public (byte Level, long Region)[] Regions { get; }

	public FloodReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		Regions = new (byte Level, long Region)[3];
		for (var i = 0; i < 3; i++)
		{
			var offset = 53 + i * 44;

			var lv = (byte)GetValue(offset, 4);
			if (i == 0 && lv == 0 || lv is < 0 or > 15)
				throw new DCReportParseException($"Lv_{i + 1} が範囲外です: " + lv);

			var pl = GetValue(offset + 4, 40);
			if (i == 0 && pl == 0 || pl is < 10175000100 or > 899999999999)
				throw new DCReportParseException($"Pl_{i + 1} が範囲外です: " + pl);

			Regions[i] = (lv, pl);
		}
	}
}
