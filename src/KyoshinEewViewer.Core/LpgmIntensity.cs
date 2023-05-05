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
	/// 長周期地震動階級0
	/// </summary>
	LpgmInt0,
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
			LpgmIntensity.LpgmInt0 => "0",
			LpgmIntensity.LpgmInt1 => "1",
			LpgmIntensity.LpgmInt2 => "2",
			LpgmIntensity.LpgmInt3 => "3",
			LpgmIntensity.LpgmInt4 => "4",
			LpgmIntensity.Error => "E",
			_ => "?",
		};

	public static LpgmIntensity ToLpgmIntensity(this string val)
		=> val switch {
			"0" => LpgmIntensity.LpgmInt0,
			"1" => LpgmIntensity.LpgmInt1,
			"2" => LpgmIntensity.LpgmInt2,
			"3" => LpgmIntensity.LpgmInt3,
			"4" => LpgmIntensity.LpgmInt4,
			_ => LpgmIntensity.Unknown,
		};
}
