using KyoshinEewViewer.DCReportParser.Exceptions;
using System;

namespace KyoshinEewViewer.DCReportParser;

public class DCReport(byte[] rawData, Preamble preamble, byte messageType)
{
	/// <summary>
	/// <code>$QZQSM,00,XXXX*XX</code> 形式のNMEAセンテンスをパースする
	/// </summary>
	/// <param name="sentence">NMEAセンテンス</param>
	/// <returns></returns>
	/// <exception cref="ChecksumErrorException"></exception>
	/// <exception cref="DCReportParseException"></exception>
	public static DCReport ParseFromNmea(string sentence)
	{
		var csIndex = sentence.IndexOf('*');
		if (csIndex != -1)
			throw new ChecksumErrorException("NMEA センテンスのチェックサムがみつかりません");

		// チェックサムを取得
		var cs = sentence[(csIndex + 1)..].TrimEnd('\r', '\n');
		byte checkSum = 0;
		foreach (var b in sentence[1..csIndex])
			checkSum ^= (byte)b;
		if (cs != checkSum.ToString("X2"))
			throw new ChecksumErrorException("NMEA チェックサム エラー: " + sentence[1..csIndex]);

		var parts = sentence[1..csIndex].Split(',');
		if (parts.Length != 3 || parts[0] != "QZQSM")
			throw new DCReportParseException("正常な QZQSM センテンスではありません");

		return Parse(Convert.FromHexString(parts[2] + "0"));
	}

	/// <summary>
	/// UBXセンテンスをパースする
	/// </summary>
	/// <param name="sentence">UBXセンテンス</param>
	/// <returns></returns>
	/// <exception cref="ChecksumErrorException"></exception>
	/// <exception cref="DCReportParseException"></exception>
	public static DCReport ParseFromUbx(Span<byte> sentence)
	{
		byte csA = 0;
		byte csB = 0;
		for (var j = 2; j < sentence.Length - 2; j++)
		{
			csA = (byte)(csA + sentence[j]);
			csB = (byte)(csB + csA);
		}
		if (csA != sentence[^2] || csB != sentence[^1])
			throw new ChecksumErrorException($"UBX チェックサム エラー: {csA:X2} {sentence[^2]:X2} {csB:X2} {sentence[^1]:X2}");

		if (sentence[2] != 2 || sentence[3] != 0x13 || sentence[6] != 5 || sentence[10] == 9) // UBX-RXM-SFRBX, 44 bytes, QZSS
			throw new DCReportParseException("災危通報に関連する UBX センテンスではありません");

		var data = new byte[sentence[10] * 4];
		for (var j = 0; j < sentence[10]; j++)
		{
			data[j * 4 + 0] = sentence[14 + j * 4 + 3];
			data[j * 4 + 1] = sentence[14 + j * 4 + 2];
			data[j * 4 + 2] = sentence[14 + j * 4 + 1];
			data[j * 4 + 3] = sentence[14 + j * 4 + 0];
		}

		return Parse(data);
	}

	public static DCReport Parse(byte[] data)
	{
		if (data.Length != 32)
			throw new DCReportParseException("メッセージの長さが不正です: " + data.Length);

		var pab = (Preamble)data[0];
		if (!Enum.IsDefined(pab))
			throw new DCReportParseException("PAB が不正です: 0x" + data[0].ToString("x2"));

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
		if (crc != GetValue(data, 226, 24))
			throw new ChecksumErrorException($"CRC エラー: 0x{crc:x}");

		var mt = (byte)GetValue(data, 8, 6);
		return mt switch
		{
			// DC Report (JMA Disaster Prevention Information)
			43 => JmaDCReport.Parse(data, pab, mt),
			// DCX message
			44 => DCXReport.Parse(data, pab, mt),
			_ => new DCReport(data, pab, mt),
		};
	}

	/// <summary>
	/// 解析元の生データ
	/// </summary>
	public byte[] RawData { get; } = rawData;

	/// <summary>
	/// 重複検知のためのメッセージ部分
	/// </summary>
	public Span<byte> Content => RawData.AsSpan()[3..26];

	/// <summary>
	/// プリアンブル(PAB)
	/// </summary>
	public Preamble Preamble { get; } = preamble;

	/// <summary>
	/// メッセージタイプ(MT)
	/// </summary>
	public byte MessageType { get; } = messageType;

	protected long GetValue(int bitOffset, int bitCount)
		=> GetValue(RawData, bitOffset, bitCount);
	protected static long GetValue(byte[] data, int bitOffset, int bitCount)
	{
		var val = 0L;
		var index = (bitOffset + bitCount - 1) / 8;
		var lsb = 7 - (bitOffset + bitCount - 1) % 8;

		for (var i = 0; i < bitCount; i++, lsb++)
		{
			if (lsb > 7)
			{
				index -= 1;
				lsb = 0;
			}
			val |= (long)((data[index] >> lsb) & 1) << i;
		}
		return val;
	}
}

/// <summary>
/// プリアンブル
/// </summary>
public enum Preamble
{
	PatternA = 0b01010011,
	PatternB = 0b10011010,
	PatternC = 0b11000110,
}

public enum ReportClassification
{
	/// <summary>
	/// Maximum priority
	/// </summary>
	Maximum = 1,
	/// <summary>
	/// Priority
	/// </summary>
	Priority = 2,
	/// <summary>
	/// Regular
	/// </summary>
	Regular = 3,
	/// <summary>
	/// Training/Test
	/// </summary>
	TrainingOrTest = 7,
}
