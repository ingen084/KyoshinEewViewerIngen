namespace KyoshinEewViewer.DCReportParser.Tests;

public static class TestUtils
{
	public static void SetCorrectCRC(byte[] data)
	{
		var crc = 0;
		for (var i = 0; i < 29; i++)
		{
			var c = data[i];
			// CRCのビットが混ざらないようにフィルタリングする
			if (i == 28)
				c &= 0xC0;
			crc ^= c << 16;

			for (var j = 0; j < 8; j++)
			{
				crc <<= 1;
				if ((crc & 0x1000000) != 0)
					crc ^= 0x1864cfb; // 生成多項式
									  // 226ビットで処理を終了させる
				if (i * 8 + j >= 225)
					break;
			}
		}
		crc &= 0xffffff;

		SetValue(data, 226, 24, crc);
	}

	public static void SetValue(byte[] data, int bitOffset, int bitCount, long value)
	{
		var index = (bitOffset + bitCount - 1) / 8;
		var lsb = 7 - (bitOffset + bitCount - 1) % 8;

		for (var i = 0; i < bitCount; i++, lsb++)
		{
			if (lsb > 7)
			{
				index -= 1;
				lsb = 0;
			}
			data[index] |= (byte)((uint)((value >> i) & 1) << lsb);
		}
	}
}
