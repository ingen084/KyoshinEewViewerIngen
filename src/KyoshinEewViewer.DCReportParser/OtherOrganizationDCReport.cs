using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser;

public class OtherOrganizationDCReport : DCReport
{
	public static OtherOrganizationDCReport Parse(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification)
	{
		var oc = (byte)GetValue(rawData, 17, 6);
		if (oc is < 1 or > 60)
			throw new DCReportParseException("Oc が範囲外です: " + oc);

		return new OtherOrganizationDCReport(rawData, preamble, messageType, reportClassification, oc);
	}

	/// <summary>
	/// 組織コード(Oc)
	/// </summary>
	public byte OrganizationCode { get; }

	public OtherOrganizationDCReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte oc) : base(rawData, preamble, messageType, reportClassification)
	{
		OrganizationCode = oc;
	}
}
