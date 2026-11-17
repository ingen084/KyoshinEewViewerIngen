namespace KyoshinEewViewer.DCReportParser.Dcx;

/// <summary>
/// 市区町村コードで対象地域を示す DCX メッセージ (L アラート、自治体発信情報)
/// </summary>
public abstract class EvacuationDCXReport(byte[] rawData, Preamble preamble, byte messageType, SatelliteDesignation satelliteDesignation, CommonAlertMessage commonAlertMessage, byte version, ushort targetAreaCode)
	: DCXReport(rawData, preamble, messageType, satelliteDesignation, commonAlertMessage, version)
{
	/// <summary>
	/// EX1 対象市区町村コード<br/>
	/// JIS X 0402 の 6 桁のコードから検査数字を除いた値。0 は指定なし
	/// </summary>
	public ushort TargetAreaCode { get; } = targetAreaCode;
}

/// <summary>
/// DCX メッセージ (L アラート)
/// </summary>
public class LAlertDCXReport(byte[] rawData, Preamble preamble, byte messageType, SatelliteDesignation satelliteDesignation, CommonAlertMessage commonAlertMessage, byte version, ushort targetAreaCode)
	: EvacuationDCXReport(rawData, preamble, messageType, satelliteDesignation, commonAlertMessage, version, targetAreaCode);

/// <summary>
/// DCX メッセージ (自治体発信情報)
/// </summary>
public class LocalGovernmentDCXReport(byte[] rawData, Preamble preamble, byte messageType, SatelliteDesignation satelliteDesignation, CommonAlertMessage commonAlertMessage, byte version, ushort targetAreaCode, AdditionalTargetArea? additionalTargetArea)
	: EvacuationDCXReport(rawData, preamble, messageType, satelliteDesignation, commonAlertMessage, version, targetAreaCode)
{
	/// <summary>
	/// EX2〜EX7 追加対象地域。指示がない場合は null
	/// </summary>
	public AdditionalTargetArea? AdditionalTargetArea { get; } = additionalTargetArea;
}

/// <summary>
/// 追加対象地域 (EX2〜EX7)
/// </summary>
/// <param name="Direction">EX2 避難方向</param>
/// <param name="Ellipse">EX3〜EX7 追加対象地域の楕円</param>
public record AdditionalTargetArea(EvacuateDirectionType Direction, DcxEllipse Ellipse);

/// <summary>
/// EX2 避難方向
/// </summary>
public enum EvacuateDirectionType
{
	/// <summary>
	/// 追加対象地域から離れる
	/// </summary>
	Leave = 0,
	/// <summary>
	/// 追加対象地域へ向かう
	/// </summary>
	Head = 1,
}
