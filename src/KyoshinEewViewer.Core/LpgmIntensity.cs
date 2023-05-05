namespace KyoshinEewViewer.Core;

/// <summary>
/// 長周期地震動階級
/// </summary>
public enum LpgmIntensity
{
	/// <summary>
	/// 不明
	/// </summary>
	Unknown,
	/// <summary>
	/// 長周期地震動階級1
	/// </summary>
	LpgmInt1,
	/// <summary>
	/// 長周期地震動階級2
	/// </summary>
	LpgmInt2,
	/// <summary>
	/// 長周期地震動階級3
	/// </summary>
	LpgmInt3,
	/// <summary>
	/// 長周期地震動階級4
	/// </summary>
	LpgmInt4,
	/// <summary>
	/// 異常
	/// </summary>
	Error,
}

public static class LpgmIntensityExtensions
{
	public static string ToShortString(this LpgmIntensity intensity)
		=> intensity switch
		{
			LpgmIntensity.LpgmInt1 => "1",
			LpgmIntensity.LpgmInt2 => "2",
			LpgmIntensity.LpgmInt3 => "3",
			LpgmIntensity.LpgmInt4 => "4",
			LpgmIntensity.Error => "E",
			_ => "?",
		};
}
