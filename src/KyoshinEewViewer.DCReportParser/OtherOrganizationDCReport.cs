using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser;

public class OtherOrganizationDCReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte oc) : DCReport(rawData, preamble, messageType)
{
	public static OtherOrganizationDCReport Parse(byte[] rawData, Preamble preamble, byte messageType)
	{
		var rc = (ReportClassification)GetValue(rawData, 14, 3);
		//if (!Enum.IsDefined(rc))
		//	throw new DCReportParseException("Rc が不正です: " + rc);

		var oc = (byte)GetValue(rawData, 17, 6);
		//if (oc is < 1 or > 60)
		//	throw new DCReportParseException("Oc が範囲外です: " + oc);

		return new OtherOrganizationDCReport(rawData, preamble, messageType, rc, oc);
	}

	/// <summary>
	/// 優先度(Rc)
	/// </summary>
	public ReportClassification ReportClassification { get; } = reportClassification;

	/// <summary>
	/// 組織コード(Oc)
	/// </summary>
	public byte OrganizationCode { get; } = oc;
}
