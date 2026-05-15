using System;

namespace KyoshinEewViewer.Series.Qzss.Services;

/// <summary>
/// シリアル等から流れてくるバイトストリームを NMEA / UBX のセンテンス単位に切り出し、
/// チェックサム検証まで通った有効なセンテンスを <see cref="SentenceDetected"/> で通知する。
/// 状態を内部に保持するためインスタンスは 1 つのストリームに対応させること。
/// </summary>
public sealed class NmeaUbxFrameParser
{
	public enum SentenceType
	{
		None,
		Nmea,
		Ubx,
	}

	/// <summary>
	/// チェックサム検証済みの有効センテンスが完成したときに発火する。
	/// payload はパーサー内部バッファを指す Span のため、ハンドラ外への退避が必要な場合はコピーすること。
	/// </summary>
	public event SentenceHandler? SentenceDetected;

	public delegate void SentenceHandler(SentenceType type, ReadOnlySpan<byte> payload);

	private SentenceType _type = SentenceType.None;
	private ushort _ubxLength;
	private int _sentenceIndex;
	private readonly byte[] _sentence = new byte[1024];

	/// <summary>
	/// 内部状態を初期化する。ポートを開き直したときなどに呼ぶ。
	/// </summary>
	public void Reset()
	{
		_type = SentenceType.None;
		_ubxLength = 0;
		_sentenceIndex = 0;
	}

	/// <summary>
	/// バイト列を投入する。完成したセンテンスがあれば <see cref="SentenceDetected"/> が発火する。
	/// </summary>
	public void Feed(ReadOnlySpan<byte> buffer)
	{
		for (var i = 0; i < buffer.Length; i++)
			FeedByte(buffer[i]);
	}

	private void FeedByte(byte c)
	{
		switch (_type)
		{
			// センテンスの開始を探す
			case SentenceType.None:
				switch (c)
				{
					// NMEA
					case (byte)'$':
						_type = SentenceType.Nmea;
						_sentence[0] = c;
						_sentenceIndex = 1;
						return;
					// UBX 同期バイト 1
					case 0xb5:
						_sentence[0] = c;
						_sentenceIndex = 1;
						return;
					// UBX 同期バイト 2
					case 0x62 when _sentenceIndex == 1 && _sentence[0] == 0xb5:
						_type = SentenceType.Ubx;
						_sentence[_sentenceIndex++] = c;
						return;
					default:
						// ノイズ。同期バイト 1 を見ていた場合はリセット
						_sentenceIndex = 0;
						return;
				}
			case SentenceType.Nmea:
				if (_sentenceIndex >= _sentence.Length)
				{
					// バッファ溢れ。捨ててリセット
					Reset();
					return;
				}
				_sentence[_sentenceIndex++] = c;
				if (c == '\n' && _sentenceIndex >= 2 && _sentence[_sentenceIndex - 2] == '\r')
				{
					if (TryValidateNmea(_sentence.AsSpan(0, _sentenceIndex)))
						SentenceDetected?.Invoke(SentenceType.Nmea, _sentence.AsSpan(0, _sentenceIndex));
					Reset();
				}
				return;
			case SentenceType.Ubx:
				if (_sentenceIndex >= _sentence.Length)
				{
					Reset();
					return;
				}
				_sentence[_sentenceIndex++] = c;
				// payload length を読む (header 4 byte + length 2 byte)
				if (_sentenceIndex == 6)
					_ubxLength = BitConverter.ToUInt16(_sentence, 4);
				else if (_sentenceIndex > 6 && _sentenceIndex >= _ubxLength + 6 + 2)
				{
					// UBX センテンスの完成
					var span = _sentence.AsSpan(0, _sentenceIndex);
					if (TryValidateUbx(span))
						SentenceDetected?.Invoke(SentenceType.Ubx, span);
					Reset();
				}
				return;
		}
	}

	/// <summary>
	/// NMEA センテンスのチェックサム (XOR) を検証する。"$" と "*XX\r\n" を含む完全なセンテンスを受け取る。
	/// </summary>
	private static bool TryValidateNmea(ReadOnlySpan<byte> sentence)
	{
		// "*XX\r\n" が末尾に必要
		if (sentence.Length < 6 || sentence[0] != (byte)'$')
			return false;
		var csIndex = sentence.LastIndexOf((byte)'*');
		if (csIndex < 0 || sentence.Length - csIndex < 5)
			return false;

		byte checkSum = 0;
		for (var i = 1; i < csIndex; i++)
			checkSum ^= sentence[i];

		var high = HexValue(sentence[csIndex + 1]);
		var low = HexValue(sentence[csIndex + 2]);
		if (high < 0 || low < 0)
			return false;
		return ((high << 4) | low) == checkSum;
	}

	private static int HexValue(byte b) => b switch
	{
		>= (byte)'0' and <= (byte)'9' => b - (byte)'0',
		>= (byte)'A' and <= (byte)'F' => b - (byte)'A' + 10,
		>= (byte)'a' and <= (byte)'f' => b - (byte)'a' + 10,
		_ => -1,
	};

	/// <summary>
	/// UBX センテンスの 8 bit Fletcher チェックサムを検証する。
	/// </summary>
	private static bool TryValidateUbx(ReadOnlySpan<byte> sentence)
	{
		// header(2) + class(1) + id(1) + length(2) + payload(N) + ck(2)
		if (sentence.Length < 8)
			return false;
		byte csA = 0;
		byte csB = 0;
		for (var j = 2; j < sentence.Length - 2; j++)
		{
			csA = (byte)(csA + sentence[j]);
			csB = (byte)(csB + csA);
		}
		return csA == sentence[^2] && csB == sentence[^1];
	}
}
