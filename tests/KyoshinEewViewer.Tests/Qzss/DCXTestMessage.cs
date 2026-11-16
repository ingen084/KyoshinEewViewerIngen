using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Dcx;

namespace KyoshinEewViewer.Tests.Qzss;

/// <summary>
/// テスト用の DCX メッセージを組み立てる
/// </summary>
public class DCXTestMessage
{
	private readonly byte[] _data = new byte[32];

	public DCXTestMessage(byte providerIdentifier, AlertMessageType messageType, byte hazard, AlertSeverity severity = AlertSeverity.Severe)
	{
		_data[0] = (byte)Preamble.PatternA;
		Set(8, 6, 44);
		Set(24, 2, (long)messageType);
		Set(26, 9, DCXReport.JapanCountryRegionCode);
		Set(35, 5, providerIdentifier);
		Set(40, 7, hazard);
		Set(47, 2, (long)severity);
		Set(66, 1, 1);
		Set(214, 6, 1);
	}

	/// <summary>
	/// 指定したビット位置に値を書き込む
	/// </summary>
	public DCXTestMessage Set(int bitOffset, int bitCount, long value)
	{
		for (var i = 0; i < bitCount; i++)
		{
			var bit = bitOffset + bitCount - 1 - i;
			var mask = (byte)(1 << (7 - bit % 8));
			if (((value >> i) & 1) == 1)
				_data[bit / 8] |= mask;
			else
				_data[bit / 8] &= (byte)~mask;
		}
		return this;
	}

	public DCXTestMessage HazardOnset(ushort timeOfWeek, HazardDuration duration)
		=> Set(50, 14, timeOfWeek).Set(64, 2, (long)duration);

	public DCXTestMessage Instruction(ushort code) => Set(70, 10, code);

	public DCXTestMessage TargetArea(ushort code) => Set(146, 16, code);

	public DCXTestMessage Prefectures(params int[] codes)
	{
		var mask = codes.Aggregate(0L, (m, c) => m | (1L << (c - 1)));
		return Set(146, 1, 0).Set(147, 47, mask);
	}

	public DCReport Parse()
	{
		var data = (byte[])_data.Clone();
		var crc = 0;
		for (var i = 0; i < 29; i++)
		{
			var c = data[i];
			if (i == 28)
				c &= 0xC0;
			crc ^= c << 16;
			for (var j = 0; j < 8; j++)
			{
				crc <<= 1;
				if ((crc & 0x1000000) != 0)
					crc ^= 0x1864cfb;
				if (i * 8 + j >= 225)
					break;
			}
		}
		crc &= 0xffffff;
		for (var i = 0; i < 24; i++)
		{
			var bit = 226 + 23 - i;
			if (((crc >> i) & 1) == 1)
				data[bit / 8] |= (byte)(1 << (7 - bit % 8));
		}
		return DCReport.Parse(data);
	}
}
