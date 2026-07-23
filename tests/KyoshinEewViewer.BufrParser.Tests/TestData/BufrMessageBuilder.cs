using KyoshinEewViewer.BufrParser.Ixac41;

namespace KyoshinEewViewer.BufrParser.Tests.TestData;

/// <summary>
/// テスト用にIXAC41形式のBUFR電文（Edition 3、Section 0〜5）を合成するビルダー。
/// 実際の記述子列・ビット配置は <see cref="EstimatedSeismicIntensityReport"/> のデコード仕様と対になっている。
/// </summary>
internal sealed class BufrMessageBuilder
{
	private readonly List<IntensityClassDefinition> _classes = [];
	private readonly List<L2Group> _l2Groups = [];

	private int _messageType;
	private DateTime _originTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
	private int _epicenterAreaCode;
	private double _hypocenterLatitude;
	private double _hypocenterLongitude;
	private double _depthKilometers;
	private int _magnitudeRaw;
	private TsunamiOption? _tsunamiOption;

	public BufrMessageBuilder AddIntensityClass(int rangeQualifier, int weakStrongFlag, int classCode, float lowerLimit, float upperLimit)
	{
		_classes.Add(new IntensityClassDefinition(rangeQualifier, weakStrongFlag, classCode, lowerLimit, upperLimit));
		return this;
	}

	public BufrMessageBuilder SetMessageType(int messageType)
	{
		_messageType = messageType;
		return this;
	}

	public BufrMessageBuilder SetOriginTimeUtc(DateTime originTimeUtc)
	{
		_originTimeUtc = originTimeUtc;
		return this;
	}

	public BufrMessageBuilder SetEpicenter(int areaCode, double latitude, double longitude, double depthKilometers, int magnitudeRaw)
	{
		_epicenterAreaCode = areaCode;
		_hypocenterLatitude = latitude;
		_hypocenterLongitude = longitude;
		_depthKilometers = depthKilometers;
		_magnitudeRaw = magnitudeRaw;
		return this;
	}

	/// <summary>
	/// 津波警報時のみ挿入される現象位置修飾ブロックを含める。
	/// </summary>
	public BufrMessageBuilder SetTsunamiOption(int qualifier, int auxPointNo, int bearing, int distance)
	{
		_tsunamiOption = new TsunamiOption(qualifier, auxPointNo, bearing, distance);
		return this;
	}

	public BufrMessageBuilder AddMesh(int m1Lat, int m1Lon, int m2Lat, int m2Lon, int m3Lat, int m3Lon, int half, int quarter, int intensityRaw)
	{
		var l2 = _l2Groups.Find(g => g.M1Lat == m1Lat && g.M1Lon == m1Lon && g.M2Lat == m2Lat && g.M2Lon == m2Lon);
		if (l2 is null)
		{
			l2 = new L2Group(m1Lat, m1Lon, m2Lat, m2Lon);
			_l2Groups.Add(l2);
		}

		var l3 = l2.L3Groups.Find(g => g.M3Lat == m3Lat && g.M3Lon == m3Lon);
		if (l3 is null)
		{
			l3 = new L3Group(m3Lat, m3Lon);
			l2.L3Groups.Add(l3);
		}

		l3.Leaves.Add(new Leaf(half, quarter, intensityRaw));
		return this;
	}

	/// <summary>
	/// Section 0〜5 を含む完全なBUFR電文を生成する。
	/// </summary>
	public byte[] Build()
	{
		var descriptorList = BuildDescriptorList();
		var sec4Body = BuildSection4Data();

		var sec1 = new byte[18];
		WriteUInt24BigEndian(sec1, 0, sec1.Length);
		sec1[7] = 0x00; // Section2なし

		var sec3 = new byte[7 + descriptorList.Length];
		sec3[4] = 0x00;
		sec3[5] = 0x01; // サブセット数=1
		descriptorList.CopyTo(sec3, 7);
		WriteUInt24BigEndian(sec3, 0, sec3.Length);

		var sec4 = new byte[4 + sec4Body.Length];
		sec4Body.CopyTo(sec4, 4);
		WriteUInt24BigEndian(sec4, 0, sec4.Length);

		var totalLength = 8 + sec1.Length + sec3.Length + sec4.Length + 4;
		var result = new byte[totalLength];
		var offset = 0;

		result[offset++] = (byte)'B';
		result[offset++] = (byte)'U';
		result[offset++] = (byte)'F';
		result[offset++] = (byte)'R';
		WriteUInt24BigEndian(result, offset, totalLength);
		offset += 3;
		result[offset++] = 3; // Edition

		sec1.CopyTo(result, offset); offset += sec1.Length;
		sec3.CopyTo(result, offset); offset += sec3.Length;
		sec4.CopyTo(result, offset); offset += sec4.Length;

		result[offset++] = (byte)'7';
		result[offset++] = (byte)'7';
		result[offset++] = (byte)'7';
		result[offset] = (byte)'7';

		return result;
	}

	/// <summary>
	/// <see cref="Build"/> の結果を単純分割する（dmdataの分割電文を模したテスト用）。
	/// </summary>
	public byte[][] BuildSplit(int chunkSize)
	{
		var full = Build();
		var chunkCount = (full.Length + chunkSize - 1) / chunkSize;
		var chunks = new byte[chunkCount][];
		for (var i = 0; i < chunkCount; i++)
		{
			var offset = i * chunkSize;
			var length = Math.Min(chunkSize, full.Length - offset);
			chunks[i] = full[offset..(offset + length)];
		}
		return chunks;
	}

	private byte[] BuildDescriptorList()
	{
		var list = new List<(byte F, byte X, byte Y)>
		{
			(1, 5, 0), (0, 31, 1), (0, 8, 193), (0, 8, 198), (0, 60, 3), (0, 60, 2), (0, 60, 2),
			(0, 1, 242),
			(3, 1, 11), (3, 1, 12),
			(0, 1, 240),
		};

		if (_tsunamiOption != null)
		{
			list.AddRange(
			[
				(0, 8, 194), (0, 1, 241), (0, 5, 21), (2, 2, 126), (0, 6, 21), (2, 2, 0),
			]);
		}

		list.AddRange(
		[
			(0, 5, 2), (0, 6, 2), (2, 2, 123), (0, 7, 61), (2, 2, 0), (0, 60, 1),
			(1, 13, 0), (0, 31, 2),
			(0, 5, 240), (0, 6, 240), (0, 5, 241), (0, 6, 241),
			(1, 7, 0), (0, 31, 1),
			(0, 5, 242), (0, 6, 242),
			(1, 3, 0), (0, 31, 3),
			(0, 5, 243), (0, 6, 243), (0, 60, 2),
		]);

		var bytes = new byte[list.Count * 2];
		for (var i = 0; i < list.Count; i++)
		{
			var (f, x, y) = list[i];
			var raw = (ushort)((f << 14) | (x << 8) | y);
			bytes[i * 2] = (byte)(raw >> 8);
			bytes[i * 2 + 1] = (byte)(raw & 0xFF);
		}
		return bytes;
	}

	private byte[] BuildSection4Data()
	{
		var writer = new BitWriter();

		writer.WriteBits((uint)_classes.Count, 8);
		foreach (var c in _classes)
		{
			writer.WriteBits((uint)c.RangeQualifier, 7);
			writer.WriteBits((uint)c.WeakStrongFlag, 2);
			writer.WriteBits((uint)c.ClassCode, 4);
			writer.WriteBits((uint)Math.Round(c.LowerLimit * 10), 7);
			writer.WriteBits((uint)Math.Round(c.UpperLimit * 10), 7);
		}

		writer.WriteBits((uint)_messageType, 7);

		writer.WriteBits((uint)_originTimeUtc.Year, 12);
		writer.WriteBits((uint)_originTimeUtc.Month, 4);
		writer.WriteBits((uint)_originTimeUtc.Day, 6);
		writer.WriteBits((uint)_originTimeUtc.Hour, 5);
		writer.WriteBits((uint)_originTimeUtc.Minute, 6);

		writer.WriteBits((uint)_epicenterAreaCode, 10);

		if (_tsunamiOption is { } tsunami)
		{
			writer.WriteBits((uint)tsunami.Qualifier, 7);
			writer.WriteBits((uint)tsunami.AuxPointNo, 10);
			writer.WriteBits((uint)tsunami.Bearing, 16);
			writer.WriteBits((uint)tsunami.Distance, 13);
		}

		writer.WriteBits((uint)Math.Round(_hypocenterLatitude * 100 + 9000), 15);
		writer.WriteBits((uint)Math.Round(_hypocenterLongitude * 100 + 18000), 16);
		writer.WriteBits((uint)Math.Round(_depthKilometers), 14);
		writer.WriteBits((uint)_magnitudeRaw, 7);

		writer.WriteBits((uint)_l2Groups.Count, 16);
		foreach (var l2 in _l2Groups)
		{
			writer.WriteBits((uint)l2.M1Lat, 7);
			writer.WriteBits((uint)l2.M1Lon, 7);
			writer.WriteBits((uint)l2.M2Lat, 4);
			writer.WriteBits((uint)l2.M2Lon, 4);

			writer.WriteBits((uint)l2.L3Groups.Count, 8);
			foreach (var l3 in l2.L3Groups)
			{
				writer.WriteBits((uint)l3.M3Lat, 4);
				writer.WriteBits((uint)l3.M3Lon, 4);

				writer.WriteBits((uint)l3.Leaves.Count, 8);
				foreach (var leaf in l3.Leaves)
				{
					writer.WriteBits((uint)leaf.Half, 3);
					writer.WriteBits((uint)leaf.Quarter, 3);
					writer.WriteBits((uint)leaf.IntensityRaw, 7);
				}
			}
		}

		return writer.ToPaddedByteArray();
	}

	private static void WriteUInt24BigEndian(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)((value >> 16) & 0xFF);
		buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
		buffer[offset + 2] = (byte)(value & 0xFF);
	}

	private sealed class L2Group(int m1Lat, int m1Lon, int m2Lat, int m2Lon)
	{
		public int M1Lat { get; } = m1Lat;
		public int M1Lon { get; } = m1Lon;
		public int M2Lat { get; } = m2Lat;
		public int M2Lon { get; } = m2Lon;
		public List<L3Group> L3Groups { get; } = [];
	}

	private sealed class L3Group(int m3Lat, int m3Lon)
	{
		public int M3Lat { get; } = m3Lat;
		public int M3Lon { get; } = m3Lon;
		public List<Leaf> Leaves { get; } = [];
	}

	private readonly record struct Leaf(int Half, int Quarter, int IntensityRaw);

	private readonly record struct TsunamiOption(int Qualifier, int AuxPointNo, int Bearing, int Distance);
}

/// <summary>
/// MSBファーストでビット単位の書き込みを行うライター（テスト用の電文合成にのみ使用）。
/// </summary>
internal sealed class BitWriter
{
	private readonly List<byte> _bytes = [];
	private int _bitBuffer;
	private int _bitCount;

	public void WriteBits(uint value, int bitCount)
	{
		for (var i = bitCount - 1; i >= 0; i--)
		{
			var bit = (value >> i) & 1;
			_bitBuffer = (_bitBuffer << 1) | (int)bit;
			_bitCount++;
			if (_bitCount != 8)
				continue;

			_bytes.Add((byte)_bitBuffer);
			_bitBuffer = 0;
			_bitCount = 0;
		}
	}

	public byte[] ToPaddedByteArray()
	{
		if (_bitCount > 0)
		{
			_bitBuffer <<= 8 - _bitCount;
			_bytes.Add((byte)_bitBuffer);
			_bitBuffer = 0;
			_bitCount = 0;
		}
		return [.. _bytes];
	}
}
