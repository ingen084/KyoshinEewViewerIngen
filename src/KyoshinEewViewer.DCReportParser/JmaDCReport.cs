using KyoshinEewViewer.DCReportParser.Exceptions;
using KyoshinEewViewer.DCReportParser.Jma;

namespace KyoshinEewViewer.DCReportParser;

public class JmaDCReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version) : DCReport(rawData, preamble, messageType)
{
	public static JmaDCReport Parse(byte[] data, Preamble pab, byte mt)
	{
		var rc = (ReportClassification)GetValue(data, 14, 3);
		if (!Enum.IsDefined(rc))
			throw new DCReportParseException("Rc が不正です: " + rc);

		var vn = (byte)GetValue(data, 214, 6);
		if (vn != 1)
			throw new DCReportParseException("この Vn には非対応です: " + vn);

		var dc = (byte)GetValue(data, 17, 4);

		var atMo = (byte)GetValue(data, 21, 4);
		if (atMo is < 1 or > 12)
			throw new DCReportParseException("AtMo が範囲外です: " + atMo);
		var atD = (byte)GetValue(data, 25, 5);
		if (atD is < 1 or > 31)
			throw new DCReportParseException("AtD が範囲外です: " + atD);
		var atH = (byte)GetValue(data, 30, 5);
		if (atH is < 0 or > 23)
			throw new DCReportParseException("AtH が範囲外です: " + atH);
		var atMi = (byte)GetValue(data, 35, 6);
		if (atMi is < 0 or > 59)
			throw new DCReportParseException("AtMi が範囲外です: " + atMi);
		var at = new DateTimeOffset(4, atMo, atD, atH, atMi, 0, TimeSpan.Zero);

		var it = (InformationType)GetValue(data, 41, 2);
		if (!Enum.IsDefined(it))
			throw new DCReportParseException("It が範囲外です: " + (int)it);

		return dc switch
		{
			// 防災気象情報(緊急地震速報)
			1 => new EewReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(震源)
			2 => new HypocenterReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(震度)
			3 => new SeismicIntensityReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(南海トラフ地震)
			4 => new NankaiTroughEarthquakeReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(津波)
			5 => new TsunamiReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(北西太平洋津波)
			6 => new NorthwestPacificTsunamiReport(data, pab, mt, rc, dc, at, it, vn),

			// 防災気象情報(火山)
			8 => new VolcanoReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(降灰)
			9 => new AshFallReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(気象)
			10 => new WeatherReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(洪水)
			11 => new FloodReport(data, pab, mt, rc, dc, at, it, vn),
			// 防災気象情報(台風)
			12 => new TyphoonReport(data, pab, mt, rc, dc, at, it, vn),

			// 防災気象情報(海上)
			14 => new MarineReport(data, pab, mt, rc, dc, at, it, vn),

			_ => new JmaDCReport(data, pab, mt, rc, dc, at, it, vn),
		};
	}

	/// <summary>
	/// 優先度(Rc)
	/// </summary>
	public ReportClassification ReportClassification { get; } = reportClassification;

	/// <summary>
	/// 災害分類コード(Dc)<br/>
	/// インスタンスが作り分けられるため基本型チェックでよい
	/// </summary>
	public byte DisasterCategoryCode { get; } = disasterCategoryCode;

	/// <summary>
	/// 発表時刻(At)<br/>
	/// パース時に基準となる日付を指定しない場合西暦は<b>4年</b>になっているので注意!
	/// </summary>
	public DateTimeOffset ReportTime { get; } = reportTime;

	/// <summary>
	/// 発表種別(It)
	/// </summary>
	public InformationType InformationType { get; } = informationType;

	/// <summary>
	/// 電文のバージョン(Vn)
	/// </summary>
	public byte Version { get; } = version;
}

public enum InformationType
{
	/// <summary>
	/// 発表
	/// </summary>
	Issue = 0,
	/// <summary>
	/// 訂正
	/// </summary>
	Correction = 1,
	/// <summary>
	/// 取消
	/// </summary>
	Cancellation = 2,
}
