namespace KyoshinEewViewer.DCReportParser.Dcx;

/// <summary>
/// 衛星指定 (SD)
/// </summary>
/// <param name="MaskType">SDMT 衛星指定マスクの種類</param>
/// <param name="Mask">SDM 衛星指定マスク (9ビット、先頭が衛星(1))</param>
public readonly record struct SatelliteDesignation(SatelliteDesignationMaskType MaskType, ushort Mask)
{
	/// <summary>
	/// 指定した衛星のビットが立っているかを取得する
	/// </summary>
	/// <param name="satellite">衛星の番号 (1〜9)</param>
	public bool IsSet(int satellite)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(satellite, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(satellite, 9);
		return ((Mask >> (9 - satellite)) & 1) == 1;
	}
}

/// <summary>
/// SDMT 衛星指定マスクの種類
/// </summary>
public enum SatelliteDesignationMaskType
{
	/// <summary>
	/// サービス種別 (0: 日本向け, 1: 海外向け)
	/// </summary>
	ServiceType = 0,
	/// <summary>
	/// MT44 の送信状態 (0: 停止中, 1: 送信中)
	/// </summary>
	TransmissionStatus = 1,
}
