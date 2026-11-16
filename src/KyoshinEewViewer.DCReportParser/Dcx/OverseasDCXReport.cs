namespace KyoshinEewViewer.DCReportParser.Dcx;

/// <summary>
/// DCX メッセージ (国外機関からの情報)
/// </summary>
public class OverseasDCXReport(byte[] rawData, Preamble preamble, byte messageType, SatelliteDesignation satelliteDesignation, CommonAlertMessage commonAlertMessage, byte version, UInt128 organizationSpecificArea)
	: DCXReport(rawData, preamble, messageType, satelliteDesignation, commonAlertMessage, version)
{
	/// <summary>
	/// EX11 国外機関固有の領域 (68ビット)。内容は未定義
	/// </summary>
	public UInt128 OrganizationSpecificArea { get; } = organizationSpecificArea;
}
