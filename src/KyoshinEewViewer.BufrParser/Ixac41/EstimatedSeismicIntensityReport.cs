using KyoshinEewViewer.BufrParser.Exceptions;
using KyoshinEewViewer.BufrParser.Primitives;

namespace KyoshinEewViewer.BufrParser.Ixac41;

/// <summary>
/// 推計震度分布図（IXAC41）電文のデコード結果。
/// </summary>
public sealed class EstimatedSeismicIntensityReport
{
	/// <summary>電文種類（0-01-242）。</summary>
	public int MessageType { get; private init; }

	/// <summary>地震発生時刻（UTC、Kind=Utc）。</summary>
	public DateTime OriginTimeUtc { get; private init; }

	/// <summary>震央地名番号（0-01-240）。</summary>
	public int EpicenterAreaCode { get; private init; }

	/// <summary>震源緯度（度）。</summary>
	public double HypocenterLatitude { get; private init; }

	/// <summary>震源経度（度）。</summary>
	public double HypocenterLongitude { get; private init; }

	/// <summary>震源の深さ（km）。</summary>
	public double DepthKilometers { get; private init; }

	/// <summary>
	/// マグニチュード。生値が127（M8超）または0（不明）の場合は null。
	/// M8超か不明かの区別が必要な場合は <see cref="IsHugeMagnitude"/> を参照する。
	/// </summary>
	public float? Magnitude { get; private init; }

	/// <summary>マグニチュードの生値が127（M8超を意味する）だったかどうか。</summary>
	public bool IsHugeMagnitude { get; private init; }

	/// <summary>階級震度テーブル。</summary>
	public IReadOnlyList<IntensityClassDefinition> IntensityClasses { get; private init; } = [];

	/// <summary>推計震度分布メッシュの一覧。</summary>
	public IReadOnlyList<EstimatedIntensityMesh> Meshes { get; private init; } = [];

	private EstimatedSeismicIntensityReport()
	{
	}

	/// <summary>
	/// BUFR電文（分割電文の場合は結合済みのもの）をデコードする。
	/// </summary>
	public static EstimatedSeismicIntensityReport Parse(ReadOnlyMemory<byte> rawMessage)
	{
		var message = BufrMessage.Parse(rawMessage);
		var cursor = new DescriptorCursor(message.Descriptors);
		var reader = message.CreateDataReader();

		var intensityClasses = ReadIntensityClassTable(cursor, reader);

		var messageType = (int)ReadField(cursor, reader, 0, 1, 242, 7);

		cursor.Expect(3, 1, 11);
		var year = (int)reader.ReadUInt32(12);
		var month = (int)reader.ReadUInt32(4);
		var day = (int)reader.ReadUInt32(6);

		cursor.Expect(3, 1, 12);
		var hour = (int)reader.ReadUInt32(5);
		var minute = (int)reader.ReadUInt32(6);

		var epicenterAreaCode = (int)ReadField(cursor, reader, 0, 1, 240, 10);

		// 津波警報時のみ、震央諸元の前に現象位置修飾のブロックが挿入される。記述子列を見て動的に判定する
		if (cursor.PeekOrNull() is { F: 0, X: 8, Y: 194 })
		{
			ReadField(cursor, reader, 0, 8, 194, 7);
			ReadField(cursor, reader, 0, 1, 241, 10);
			ReadField(cursor, reader, 0, 5, 21, 16);
			cursor.Expect(2, 2, 126);
			ReadField(cursor, reader, 0, 6, 21, 13);
			cursor.Expect(2, 2, 0);
		}

		var latRaw = (int)ReadField(cursor, reader, 0, 5, 2, 15);
		var lonRaw = (int)ReadField(cursor, reader, 0, 6, 2, 16);
		var hypocenterLatitude = (latRaw - 9000) / 100.0;
		var hypocenterLongitude = (lonRaw - 18000) / 100.0;

		cursor.Expect(2, 2, 123);
		var depthRaw = ReadField(cursor, reader, 0, 7, 61, 14);
		// 2-02-123 のスケール変更により、生値はそのままkm単位を表す（実サンプルで検証済み）
		var depthKilometers = (double)depthRaw;
		cursor.Expect(2, 2, 0);

		var magnitudeRaw = ReadField(cursor, reader, 0, 60, 1, 7);
		var isHugeMagnitude = magnitudeRaw == 127;
		float? magnitude = magnitudeRaw switch
		{
			127 => null, // M8超
			0 => null, // 不明
			_ => magnitudeRaw / 10f,
		};

		var originTimeUtc = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);

		var meshes = ReadMeshes(cursor, reader);

		return new EstimatedSeismicIntensityReport
		{
			MessageType = messageType,
			OriginTimeUtc = originTimeUtc,
			EpicenterAreaCode = epicenterAreaCode,
			HypocenterLatitude = hypocenterLatitude,
			HypocenterLongitude = hypocenterLongitude,
			DepthKilometers = depthKilometers,
			Magnitude = magnitude,
			IsHugeMagnitude = isHugeMagnitude,
			IntensityClasses = intensityClasses,
			Meshes = meshes,
		};
	}

	/// <summary>
	/// BUFR電文のデコードを試みる。失敗した場合は false を返し、<paramref name="error"/> にエラーメッセージを格納する。
	/// </summary>
	public static bool TryParse(ReadOnlyMemory<byte> rawMessage, out EstimatedSeismicIntensityReport? report, out string? error)
	{
		try
		{
			report = Parse(rawMessage);
			error = null;
			return true;
		}
		catch (BufrParseException ex)
		{
			report = null;
			error = ex.Message;
			return false;
		}
	}

	private static List<IntensityClassDefinition> ReadIntensityClassTable(DescriptorCursor cursor, BitReader reader)
	{
		var replication = ExpectReplication(cursor);
		var count = ReadDelayedCount(cursor, reader);
		var groupStart = cursor.Index;

		var classes = new List<IntensityClassDefinition>(count);
		for (var i = 0; i < count; i++)
		{
			cursor.Index = groupStart;
			var rangeQualifier = (int)ReadField(cursor, reader, 0, 8, 193, 7);
			var weakStrongFlag = (int)ReadField(cursor, reader, 0, 8, 198, 2);
			var classCode = (int)ReadField(cursor, reader, 0, 60, 3, 4);
			var lowerLimit = ReadField(cursor, reader, 0, 60, 2, 7) / 10f;
			var upperLimit = ReadField(cursor, reader, 0, 60, 2, 7) / 10f;
			classes.Add(new IntensityClassDefinition(rangeQualifier, weakStrongFlag, classCode, lowerLimit, upperLimit));
		}
		cursor.Index = groupStart + replication.X;

		return classes;
	}

	private static List<EstimatedIntensityMesh> ReadMeshes(DescriptorCursor cursor, BitReader reader)
	{
		var meshes = new List<EstimatedIntensityMesh>();

		var l2Replication = ExpectReplication(cursor);
		var l2Count = ReadDelayedCount(cursor, reader);
		var l2GroupStart = cursor.Index;

		for (var i = 0; i < l2Count; i++)
		{
			cursor.Index = l2GroupStart;
			var m1Lat = (int)ReadField(cursor, reader, 0, 5, 240, 7);
			var m1Lon = (int)ReadField(cursor, reader, 0, 6, 240, 7);
			var m2Lat = (int)ReadField(cursor, reader, 0, 5, 241, 4);
			var m2Lon = (int)ReadField(cursor, reader, 0, 6, 241, 4);

			var l3Replication = ExpectReplication(cursor);
			var l3Count = ReadDelayedCount(cursor, reader);
			var l3GroupStart = cursor.Index;

			for (var j = 0; j < l3Count; j++)
			{
				cursor.Index = l3GroupStart;
				var m3Lat = (int)ReadField(cursor, reader, 0, 5, 242, 4);
				var m3Lon = (int)ReadField(cursor, reader, 0, 6, 242, 4);

				var l4Replication = ExpectReplication(cursor);
				var l4Count = ReadDelayedCount(cursor, reader);
				var l4GroupStart = cursor.Index;

				for (var k = 0; k < l4Count; k++)
				{
					cursor.Index = l4GroupStart;
					var half = (int)ReadField(cursor, reader, 0, 5, 243, 3);
					var quarter = (int)ReadField(cursor, reader, 0, 6, 243, 3);
					var intensityRaw = ReadField(cursor, reader, 0, 60, 2, 7);

					var (lat, lon) = MeshCodeResolver.ResolveSouthWest(m1Lat, m1Lon, m2Lat, m2Lon, m3Lat, m3Lon, half, quarter);
					meshes.Add(new EstimatedIntensityMesh(lat, lon, intensityRaw / 10f));
				}
				cursor.Index = l4GroupStart + l4Replication.X;
			}
			cursor.Index = l3GroupStart + l3Replication.X;
		}
		cursor.Index = l2GroupStart + l2Replication.X;

		return meshes;
	}

	/// <summary>
	/// 記述子列の期待値を検証しつつビットを読み取る。
	/// </summary>
	private static uint ReadField(DescriptorCursor cursor, BitReader reader, byte f, byte x, byte y, int bitCount)
	{
		cursor.Expect(f, x, y);
		return reader.ReadUInt32(bitCount);
	}

	/// <summary>
	/// 遅延反復記述子（1-XX-000）を読み取る。反復対象の記述子数(X)は呼び出し側でグループ長として利用する。
	/// </summary>
	private static BufrDescriptor ExpectReplication(DescriptorCursor cursor)
	{
		var descriptor = cursor.PeekOrNull()
			?? throw new BufrParseException("記述子列の終端に達しました。遅延反復記述子(1-XX-000)が必要です。");
		if (descriptor.F != 1 || descriptor.Y != 0)
			throw new BufrParseException($"未知の記述子です。遅延反復記述子(1-XX-000)を期待しましたが実際は {descriptor} でした。");

		cursor.Index++;
		return descriptor;
	}

	/// <summary>
	/// 遅延反復カウント記述子（0-31-001/002/003）を読み取り、反復回数を返す。
	/// </summary>
	private static int ReadDelayedCount(DescriptorCursor cursor, BitReader reader)
	{
		var descriptor = cursor.PeekOrNull()
			?? throw new BufrParseException("記述子列の終端に達しました。遅延反復カウント記述子(0-31-00N)が必要です。");
		if (descriptor.F != 0 || descriptor.X != 31)
			throw new BufrParseException($"未知の記述子です。遅延反復カウント記述子(0-31-00N)を期待しましたが実際は {descriptor} でした。");

		var bitCount = descriptor.Y switch
		{
			1 => 8,
			2 => 16,
			3 => 8,
			_ => throw new BufrParseException($"未対応の遅延反復カウント記述子です: {descriptor}"),
		};
		cursor.Index++;
		return (int)reader.ReadUInt32(bitCount);
	}

	/// <summary>
	/// Section 3 の記述子列を先頭から順に消費するためのカーソル。
	/// </summary>
	private sealed class DescriptorCursor(IReadOnlyList<BufrDescriptor> descriptors)
	{
		public int Index { get; set; }

		public BufrDescriptor Expect(byte f, byte x, byte y)
		{
			if (Index >= descriptors.Count)
				throw new BufrParseException($"記述子列の終端に達しました。期待する記述子: {new BufrDescriptor(f, x, y)}");

			var actual = descriptors[Index];
			if (actual.F != f || actual.X != x || actual.Y != y)
				throw new BufrParseException($"未知の記述子です。期待: {new BufrDescriptor(f, x, y)} 実際: {actual}");

			Index++;
			return actual;
		}

		public BufrDescriptor? PeekOrNull() => Index < descriptors.Count ? descriptors[Index] : null;
	}
}
