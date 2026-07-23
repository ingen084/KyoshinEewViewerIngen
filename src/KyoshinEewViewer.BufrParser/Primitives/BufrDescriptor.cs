namespace KyoshinEewViewer.BufrParser.Primitives;

/// <summary>
/// BUFR記述子（FXY形式）。F: 2bit、X: 6bit、Y: 8bit で構成される。
/// </summary>
public readonly record struct BufrDescriptor(byte F, byte X, byte Y)
{
	/// <summary>
	/// 記述子表に格納されている2バイトの生値からBufrDescriptorを構築する。
	/// </summary>
	public static BufrDescriptor FromRaw(ushort raw)
	{
		var f = (byte)((raw >> 14) & 0b11);
		var x = (byte)((raw >> 8) & 0b0011_1111);
		var y = (byte)(raw & 0xFF);
		return new BufrDescriptor(f, x, y);
	}

	public override string ToString() => $"{F}-{X:D2}-{Y:D3}";
}
