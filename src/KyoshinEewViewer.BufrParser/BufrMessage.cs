using KyoshinEewViewer.BufrParser.Exceptions;
using KyoshinEewViewer.BufrParser.Primitives;

namespace KyoshinEewViewer.BufrParser;

/// <summary>
/// BUFR Edition 3 電文の共通セクション（Section 0/1/2/3/4/5）を解析する。
/// Section 4 のデータ本体はメッセージ種別ごとの専用パーサ（<see cref="Ixac41.EstimatedSeismicIntensityReport"/> など）が
/// <see cref="Descriptors"/> を先頭から解釈しながら読み進める。
/// </summary>
public sealed class BufrMessage
{
	private readonly ReadOnlyMemory<byte> _raw;
	private readonly int _dataStartByteOffset;

	/// <summary>
	/// BUFR版番号。KyoshinEewViewer.BufrParser は Edition 3 のみ対応する。
	/// </summary>
	public int Edition { get; }

	/// <summary>
	/// Section 3 に格納されている記述子列。
	/// </summary>
	public IReadOnlyList<BufrDescriptor> Descriptors { get; }

	private BufrMessage(ReadOnlyMemory<byte> raw, int edition, IReadOnlyList<BufrDescriptor> descriptors, int dataStartByteOffset)
	{
		_raw = raw;
		Edition = edition;
		Descriptors = descriptors;
		_dataStartByteOffset = dataStartByteOffset;
	}

	/// <summary>
	/// Section 4 のデータ本体先頭にシークした <see cref="BitReader"/> を生成する。
	/// </summary>
	public BitReader CreateDataReader() => new(_raw, _dataStartByteOffset * 8);

	public static BufrMessage Parse(ReadOnlyMemory<byte> raw)
	{
		var span = raw.Span;

		// Section 0: "BUFR"(4B) + 全長(3B BE) + 版(1B)
		if (span.Length < 8)
			throw new BufrParseException("BUFR電文が短すぎます。Section 0 を読み取れません。");
		if (span[0] != (byte)'B' || span[1] != (byte)'U' || span[2] != (byte)'F' || span[3] != (byte)'R')
			throw new BufrParseException("BUFR電文のマジックバイトが不正です。'BUFR' で始まっていません。");

		var declaredLength = ReadUInt24BigEndian(span, 4);
		if (declaredLength != span.Length)
			throw new BufrParseException($"BUFR電文の宣言長({declaredLength})が実際のデータ長({span.Length})と一致しません。分割電文が正しく結合されていない可能性があります。");

		var edition = span[7];
		if (edition != 3)
			throw new BufrParseException($"未対応のBUFR版です: Edition {edition}（対応しているのは Edition 3 のみ）。");

		var offset = 8;

		// Section 1: 長さ(3B BE) で読み飛ばす。offset+7 のフラグ 0x80 で Section 2 有無を判定
		if (offset + 3 > span.Length)
			throw new BufrParseException("Section 1 の長さを読み取れません。電文が途中で途切れています。");
		var sec1Length = ReadUInt24BigEndian(span, offset);
		if (sec1Length < 8 || offset + sec1Length > span.Length)
			throw new BufrParseException("Section 1 の長さが電文の範囲を超えています。");
		var hasSection2 = (span[offset + 7] & 0x80) != 0;
		offset += sec1Length;

		if (hasSection2)
		{
			if (offset + 3 > span.Length)
				throw new BufrParseException("Section 2 の長さを読み取れません。電文が途中で途切れています。");
			var sec2Length = ReadUInt24BigEndian(span, offset);
			if (sec2Length < 3 || offset + sec2Length > span.Length)
				throw new BufrParseException("Section 2 の長さが電文の範囲を超えています。");
			offset += sec2Length;
		}

		// Section 3: 長さ(3B) + 予備(1B) + サブセット数(2B) + フラグ(1B) + 記述子列(各2B BE)
		if (offset + 3 > span.Length)
			throw new BufrParseException("Section 3 の長さを読み取れません。電文が途中で途切れています。");
		var sec3Start = offset;
		var sec3Length = ReadUInt24BigEndian(span, offset);
		if (sec3Length < 7 || offset + sec3Length > span.Length)
			throw new BufrParseException("Section 3 の長さが電文の範囲を超えています。");
		var descriptors = ParseDescriptors(span.Slice(sec3Start + 7, sec3Length - 7));
		offset += sec3Length;

		// Section 4: 長さ(3B) + 予備(1B) + データ本体
		if (offset + 4 > span.Length)
			throw new BufrParseException("Section 4 の長さを読み取れません。電文が途中で途切れています。");
		var sec4Length = ReadUInt24BigEndian(span, offset);
		if (sec4Length < 4 || offset + sec4Length > span.Length)
			throw new BufrParseException("Section 4 の長さが電文の範囲を超えています。");
		var dataStartByteOffset = offset + 4;
		offset += sec4Length;

		// Section 5: "7777" 検証
		if (offset + 4 > span.Length ||
			span[offset] != (byte)'7' || span[offset + 1] != (byte)'7' ||
			span[offset + 2] != (byte)'7' || span[offset + 3] != (byte)'7')
			throw new BufrParseException("Section 5 の終端マーカー '7777' が見つかりません。電文が破損しているか途中で途切れています。");

		return new BufrMessage(raw, edition, descriptors, dataStartByteOffset);
	}

	private static List<BufrDescriptor> ParseDescriptors(ReadOnlySpan<byte> descriptorBytes)
	{
		var pairCount = descriptorBytes.Length / 2;
		var descriptors = new List<BufrDescriptor>(pairCount);
		for (var i = 0; i < pairCount; i++)
		{
			var raw = (ushort)((descriptorBytes[i * 2] << 8) | descriptorBytes[i * 2 + 1]);
			descriptors.Add(BufrDescriptor.FromRaw(raw));
		}
		return descriptors;
	}

	private static int ReadUInt24BigEndian(ReadOnlySpan<byte> span, int offset)
		=> (span[offset] << 16) | (span[offset + 1] << 8) | span[offset + 2];
}
