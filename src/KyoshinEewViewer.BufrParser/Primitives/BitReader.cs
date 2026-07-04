using KyoshinEewViewer.BufrParser.Exceptions;

namespace KyoshinEewViewer.BufrParser.Primitives;

/// <summary>
/// MSBファーストでビット単位の読み取りを行うリーダー。BUFR電文のデータ部（Section 4）のデコードに使用する。
/// </summary>
public sealed class BitReader
{
	private readonly ReadOnlyMemory<byte> _data;

	/// <summary>
	/// 現在の読み取り位置（先頭からのビット数）。
	/// </summary>
	public int BitPosition { get; private set; }

	public BitReader(ReadOnlyMemory<byte> data, int startBitPosition = 0)
	{
		_data = data;
		BitPosition = startBitPosition;
	}

	/// <summary>
	/// 指定したビット数を読み取り、符号なし整数として返す。バイト境界を跨ぐ読み取りにも対応する。
	/// </summary>
	/// <param name="bitCount">読み取るビット数（0〜32）。</param>
	public uint ReadUInt32(int bitCount)
	{
		if (bitCount is < 0 or > 32)
			throw new BufrParseException($"ビット読み取り幅が不正です: {bitCount}");
		if (bitCount == 0)
			return 0;

		var span = _data.Span;
		if (BitPosition + bitCount > span.Length * 8)
			throw new BufrParseException("BUFR電文の末尾を超えてビットを読み取ろうとしました。");

		uint value = 0;
		var remaining = bitCount;
		var bitPos = BitPosition;
		while (remaining > 0)
		{
			var byteIndex = bitPos / 8;
			var bitOffsetInByte = bitPos % 8;
			var bitsAvailableInByte = 8 - bitOffsetInByte;
			var bitsToRead = Math.Min(bitsAvailableInByte, remaining);

			var shift = bitsAvailableInByte - bitsToRead;
			var mask = (1 << bitsToRead) - 1;
			var bits = (span[byteIndex] >> shift) & mask;

			value = (value << bitsToRead) | (uint)bits;

			bitPos += bitsToRead;
			remaining -= bitsToRead;
		}

		BitPosition = bitPos;
		return value;
	}
}
