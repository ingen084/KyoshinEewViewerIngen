using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Jma;

public class WeatherReport : JmaDCReport
{
	/// <summary>
	/// 警報状況(Ar)
	/// </summary>
	public byte WarningState { get; }

	/// <summary>
	/// 警報の詳細、警報地域(Ww,Pl)
	/// </summary>
	public (byte SubCategory, int Region)[] Regions { get; }

	public WeatherReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : base(rawData, preamble, messageType, reportClassification, disasterCategoryCode, reportTime, informationType, version)
	{
		WarningState = (byte)GetValue(53, 3);

		Regions = new (byte SubCategory, int Region)[6];
		for (var i = 0; i < 6; i++)
		{
			var offset = 56 + i * 24;

			var ww = (byte)GetValue(offset, 5);
			if (i == 0 && ww == 0 || ww is < 0 or > 31)
				throw new DCReportParseException($"Ww_{i + 1} が範囲外です: " + ww);

			var pl = GetValue(offset + 5, 19);
			if (i == 0 && pl == 0 || pl is not 0 and (< 11000 or > 500000))
				throw new DCReportParseException($"Pl_{i + 1} が範囲外です: " + ww);

			Regions[i] = (ww, (int)pl);
		}
	}
}
