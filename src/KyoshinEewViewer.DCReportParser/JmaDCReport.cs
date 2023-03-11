using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser;

public class JmaDCReport
{
	/// <summary>
	/// <code>$QZQSM,00,XXXX*XX</code> 形式のNMEAセンテンスをパースする
	/// </summary>
	/// <param name="sentence">NMEAセンテンス</param>
	/// <returns></returns>
	/// <exception cref="ChecksumErrorException"></exception>
	/// <exception cref="DCReportParseException"></exception>
	public static JmaDCReport ParseFromNmea(string sentence)
	{
		// チェックサム確認
		var csIndex = sentence.IndexOf('*');
		if (csIndex != -1)
			throw new DCReportParseException("NMEA センテンスのチェックサムがみつかりません");

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

		return Parse(Convert.FromHexString(parts[2]));
	}

	/// <summary>
	/// UBXセンテンスをパースする
	/// </summary>
	/// <param name="sentence">UBXセンテンス</param>
	/// <returns></returns>
	/// <exception cref="ChecksumErrorException"></exception>
	/// <exception cref="DCReportParseException"></exception>
	public static JmaDCReport ParseFromUbx(Span<byte> sentence)
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

	public static JmaDCReport Parse(byte[] data)
	{
		if (data.Length != 32)
			throw new DCReportParseException("メッセージの長さが不正です: " + data.Length);

		var preamble = data[0] switch
		{
			(byte)Preamble.PatternA => Preamble.PatternA,
			(byte)Preamble.PatternB => Preamble.PatternB,
			(byte)Preamble.PatternC => Preamble.PatternC,
			_ => throw new DCReportParseException("PAB が不正です: 0x" + data[0].ToString("x2")),
		};
		var messageType = (byte)GetValue(data, 8, 6);
		if (messageType != 43)
			throw new DCReportParseException("Jma DC Report ではありません: " + messageType);

		var rc = (byte)GetValue(data, 14, 3);
		var reportClassification = rc switch
		{
			(byte)ReportClassification.Maximum => ReportClassification.Maximum,
			(byte)ReportClassification.Priority => ReportClassification.Priority,
			(byte)ReportClassification.Regular => ReportClassification.Regular,
			(byte)ReportClassification.TrainingOrTest => ReportClassification.TrainingOrTest,
			_ => throw new DCReportParseException("Rc が不正です: " + rc),
		};

		var dc = (byte)GetValue(data, 17, 4);

		var atMo = (byte)GetValue(data, 21, 4);
		if (atMo is < 1 or > 12)
			throw new DCReportParseException("AtMo が範囲外です: " + atMo);
		var atD = (byte)GetValue(data, 25, 5);
		if (atD is < 1 or > 31)
			throw new DCReportParseException("AtD が範囲外です: " + atD);
		var atH = (byte)GetValue(data, 30, 5);
		if (atH is < 0 or > 23)
			throw new DCReportParseException("AtH が範囲外です: " + atH);
		var atMi = (byte)GetValue(data, 35, 6);
		if (atMi is < 0 or > 59)
			throw new DCReportParseException("AtMi が範囲外です: " + atMi);
		var reportTime = new DateTimeOffset(4, atMo, atD, atH, atMi, 0, TimeSpan.Zero);

		var informationType = (InformationType)GetValue(data, 41, 2);
		if (!Enum.IsDefined(informationType))
			throw new DCReportParseException("It が範囲外です: " + (int)informationType);

		var version = (byte)GetValue(data, 214, 6);
		if (version != 1)
			throw new DCReportParseException("この Vn には非対応です: " + version);

		return dc switch
		{
			// 防災気象情報(緊急地震速報)
			1 => new EewReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(震源)
			2 => new HypocenterReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(震度)
			3 => new SeismicIntensityReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(南海トラフ地震)
			4 => new NankaiTroughEarthquakeReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(津波)
			5 => new TsunamiReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(北西太平洋津波)
			6 => new NorthwestPacificTsunamiReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),

			// 防災気象情報(火山)
			8 => new VolcanoReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(降灰)
			9 => new AshFallReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(気象)
			10 => new WeatherReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(洪水)
			11 => new FloodReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
			// 防災気象情報(台風)
			12 => new TyphoonReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),

			// 防災気象情報(海上)
			14 => new MarineReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),

			_ => new JmaDCReport(data, preamble, messageType, reportClassification, dc, reportTime, informationType, version),
		};
	}

	/// <summary>
	/// 解析元の生データ
	/// </summary>
	public byte[] RawData { get; }

	/// <summary>
	/// プリアンブル(PAB)
	/// </summary>
	public Preamble Preamble { get; }

	/// <summary>
	/// メッセージタイプ(MT)
	/// </summary>
	public byte MessageType { get; }

	/// <summary>
	/// 優先度(Rc)
	/// </summary>
	public ReportClassification ReportClassification { get; }

	/// <summary>
	/// 災害分類コード(Dc)<br/>
	/// インスタンスが作り分けられるため基本型チェックでよい
	/// </summary>
	public byte DisasterCategoryCode { get; }

	/// <summary>
	/// 発表時刻(At)<br/>
	/// パース時に基準となる日付を指定しない場合西暦は<b>4年</b>になっているので注意!
	/// </summary>
	public DateTimeOffset ReportTime { get; }

	/// <summary>
	/// 発表種別(It)
	/// </summary>
	public InformationType InformationType { get; }

	/// <summary>
	/// 電文のバージョン(Vn)
	/// </summary>
	public byte Version { get; }

	public JmaDCReport(byte[] rawData, Preamble preamble, byte messageType, ReportClassification reportClassification, byte disasterCategoryCode, DateTimeOffset reportTime, InformationType informationType, byte version)
	{
		RawData = rawData;
		Preamble = preamble;
		MessageType = messageType;
		ReportClassification = reportClassification;
		DisasterCategoryCode = disasterCategoryCode;
		ReportTime = reportTime;
		InformationType = informationType;
		Version = version;
	}

	protected long GetValue(int bitOffset, int bitCount)
		=> GetValue(RawData, bitOffset, bitCount);
	private static long GetValue(byte[] data, int bitOffset, int bitCount)
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
			val |= (uint)((data[index] >> lsb) & 1) << i;
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

public enum InformationType
{
	/// <summary>
	/// 発表
	/// </summary>
	Issue = 0,
	/// <summary>
	/// 訂正
	/// </summary>
	Correction = 1,
	/// <summary>
	/// 取消
	/// </summary>
	Cancellation = 2,
}
