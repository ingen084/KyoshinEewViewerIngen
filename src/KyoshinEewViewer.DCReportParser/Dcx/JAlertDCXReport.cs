namespace KyoshinEewViewer.DCReportParser.Dcx;

/// <summary>
/// DCX メッセージ (J アラート)
/// </summary>
public class JAlertDCXReport(byte[] rawData, Preamble preamble, byte messageType, SatelliteDesignation satelliteDesignation, CommonAlertMessage commonAlertMessage, byte version, TargetAreaCodeListType targetAreaCodeListType, ulong prefectureMask, ushort[] municipalityCodes)
	: DCXReport(rawData, preamble, messageType, satelliteDesignation, commonAlertMessage, version)
{
	/// <summary>
	/// 全都道府県を示す EX9 の値
	/// </summary>
	public const ulong AllPrefecturesMask = (1UL << 47) - 1;

	/// <summary>
	/// EX8 対象地域コードリストの種類
	/// </summary>
	public TargetAreaCodeListType TargetAreaCodeListType { get; } = targetAreaCodeListType;

	/// <summary>
	/// EX9 対象都道府県のビットマスク (最下位ビットが北海道、47ビット目が沖縄)<br/>
	/// EX8 が都道府県コードの場合のみ有効
	/// </summary>
	public ulong PrefectureMask { get; } = prefectureMask;

	/// <summary>
	/// 対象都道府県のコード (JIS X 0401、1: 北海道 〜 47: 沖縄県)
	/// </summary>
	public IEnumerable<int> PrefectureCodes
		=> Enumerable.Range(1, 47).Where(c => ((PrefectureMask >> (c - 1)) & 1) == 1);

	/// <summary>
	/// EX9 対象市区町村コード (0 を除いた最大 4 件)<br/>
	/// EX8 が市区町村コードの場合のみ有効。値の形式は <see cref="EvacuationDCXReport.TargetAreaCode"/> と同じ
	/// </summary>
	public ushort[] MunicipalityCodes { get; } = municipalityCodes;
}

/// <summary>
/// EX8 対象地域コードリストの種類
/// </summary>
public enum TargetAreaCodeListType
{
	/// <summary>
	/// 都道府県コード
	/// </summary>
	Prefecture = 0,
	/// <summary>
	/// 市区町村コード
	/// </summary>
	Municipality = 1,
}
