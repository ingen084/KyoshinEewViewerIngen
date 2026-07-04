using KyoshinEewViewer.BufrParser.Exceptions;
using KyoshinEewViewer.BufrParser.Primitives;
using Xunit;

namespace KyoshinEewViewer.BufrParser.Tests;

public class BitReaderTests
{
	[Theory(DisplayName = "ReadUInt32はバイト境界を跨ぐ場合を含めMSBファーストで正しい値を返す")]
	[InlineData(new byte[] { 0b1010_1100, 0b1111_0000 }, 0, 4, 0b1010u)]
	[InlineData(new byte[] { 0b1010_1100, 0b1111_0000 }, 4, 8, 0b1100_1111u)]
	[InlineData(new byte[] { 0b1010_1100, 0b1111_0000 }, 6, 4, 0b0011u)]
	[InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, 0, 32, 0xFFFFFFFFu)]
	[InlineData(new byte[] { 0x00, 0x00, 0x00, 0x01 }, 24, 8, 0x01u)]
	[InlineData(new byte[] { 0x12, 0x34, 0x56 }, 0, 0, 0u)]
	public void ReadUInt32_様々なビット幅とオフセットで正しく読み取れる(byte[] data, int startBitPosition, int bitCount, uint expected)
	{
		var reader = new BitReader(data, startBitPosition);
		Assert.Equal(expected, reader.ReadUInt32(bitCount));
		Assert.Equal(startBitPosition + bitCount, reader.BitPosition);
	}

	[Fact(DisplayName = "連続してビットを読み取ると読み取り位置が正しく進む")]
	public void ReadUInt32_連続読み取りで位置が正しく進む()
	{
		byte[] data = [0b1101_0110, 0b0011_1010];
		var reader = new BitReader(data);

		Assert.Equal(0b1101u, reader.ReadUInt32(4));
		Assert.Equal(0b0110_0011u, reader.ReadUInt32(8));
		Assert.Equal(0b1010u, reader.ReadUInt32(4));
	}

	[Fact(DisplayName = "データ末尾を超える読み取りやビット幅超過はBufrParseExceptionを送出する")]
	public void ReadUInt32_不正な読み取りは例外を送出する()
	{
		byte[] data = [0xFF, 0xFF];
		var reader = new BitReader(data);

		Assert.Throws<BufrParseException>(() => reader.ReadUInt32(17));
		Assert.Throws<BufrParseException>(() => new BitReader(data).ReadUInt32(33));
	}
}
