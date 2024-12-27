using MessagePack;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core.Models.EarthquakeReplay;

public class KyoshinReplayFileReader(Stream stream) : IDisposable
{
	private static ReadOnlySpan<byte> HeaderMagic => "EQRP"u8;

	private readonly Stream stream = stream;

	public async Task<ReplayFileHeader> ReadHeader()
	{
		stream.Seek(0, SeekOrigin.Begin);
		var buffer = new byte[HeaderMagic.Length];
		if ((await stream.ReadAsync(buffer.AsMemory())) < HeaderMagic.Length || !HeaderMagic.SequenceEqual(buffer))
			throw new InvalidDataException("Magic Header の読み込みができませんでした");

		var header = MessagePackSerializer.Deserialize<ReplayFileHeader>(stream);
		if (header.Version != 0)
			throw new InvalidDataException($"未対応のバージョンです({header.Version})");
		return header;
	}

	public async Task<ReplayData[]> ReadData(ReplayFileCompressionMode compressionMode)
	{
		var option = MessagePackSerializerOptions.Standard;
		switch (compressionMode)
		{
			case ReplayFileCompressionMode.GZip:
				using (var gzip = new GZipStream(stream, CompressionMode.Decompress, true))
					return await MessagePackSerializer.DeserializeAsync<ReplayData[]>(gzip, option);
			case ReplayFileCompressionMode.Brotil:
				using (var brotli = new BrotliStream(stream, CompressionMode.Decompress, true))
					return await MessagePackSerializer.DeserializeAsync<ReplayData[]>(brotli, option);
			default:
				if (compressionMode == ReplayFileCompressionMode.MessagePackCSharpLz4BlockArray)
					option = option.WithCompression(MessagePackCompression.Lz4BlockArray);
				return await MessagePackSerializer.DeserializeAsync<ReplayData[]>(stream, option);
		}
	}

	public async Task WriteHeader(ReplayFileHeader header)
	{
		stream.Seek(0, SeekOrigin.Begin);
		await stream.WriteAsync(HeaderMagic.ToArray());
		await MessagePackSerializer.SerializeAsync(stream, header);
	}

	public async Task WriteData(ReplayData[] data, ReplayFileCompressionMode compressionMode)
	{
		var option = MessagePackSerializerOptions.Standard;
		switch (compressionMode)
		{
			case ReplayFileCompressionMode.GZip:
				using (var gzip = new GZipStream(stream, CompressionLevel.SmallestSize, true))
					await MessagePackSerializer.SerializeAsync(gzip, data, option);
				break;
			case ReplayFileCompressionMode.Brotil:
				using (var brotli = new BrotliStream(stream, CompressionLevel.SmallestSize, true))
					await MessagePackSerializer.SerializeAsync(brotli, data, option);
				break;
			default:
				if (compressionMode == ReplayFileCompressionMode.MessagePackCSharpLz4BlockArray)
					option = option.WithCompression(MessagePackCompression.Lz4BlockArray);
				await MessagePackSerializer.SerializeAsync(stream, data, option);
				break;
		}
	}

	public void Dispose()
	{
		stream.Dispose();
		GC.SuppressFinalize(this);
	}
}

[MessagePackObject]
public class ReplayFileHeader
{
	[Key(0)]
	public uint Version { get; set; } = 0;

	[Key(1)]
	public string SoftwareName { get; set; }

	[Key(2)]
	public DateTime StartTime { get; set; }

	[Key(3)]
	public DateTime EndTime { get; set; }

	[Key(4)]
	public ReplayFileCompressionMode CompressionMode { get; set; }

}
public enum ReplayFileCompressionMode
{
	None = 0,
	MessagePackCSharpLz4BlockArray = 1,
	GZip = 2,
	Brotil = 3,
}

[Union(0, typeof(JmaXmlTelegramReplayData))]
[Union(100, typeof(KyoshinMonitorImageReplayData))]
[Union(101, typeof(KyoshinMonitorEewJsonReplayData))]
[Union(1000, typeof(KEViJsonReplayData))]
[Union(1001, typeof(SNPLogEntryReplayData))]
[Union(1002, typeof(AxisJsonReplayData))]
public abstract class ReplayData
{
	[Key(0)]
	public DateTime Time { get; set; }
}

[MessagePackObject]
public class JmaXmlTelegramReplayData : ReplayData
{
	[Key(1)]
	public string Title { get; set; }

	[Key(2)]
	public string Telegram { get; set; }
}

[MessagePackObject]
public class JmaBinaryTelegramReplayData : ReplayData
{
	[Key(1)]
	public string Type { get; set; }

	[Key(2)]
	public byte[] Data { get; set; }
}

[MessagePackObject]
public class KyoshinMonitorImageReplayData : ReplayData
{
	[Key(1)]
	public Dictionary<ImageType, byte[]> Images { get; set; }

	/// <summary>
	/// リアルタイム画像の種類
	/// </summary>
	public enum ImageType
	{
		Shindo = 0,
		Pga = 1,
		Pgv = 2,
		PSWave = 3,
		EstShindo = 4,
	}
}

[MessagePackObject]
public class KyoshinMonitorEewJsonReplayData : ReplayData
{
	[Key(1)]
	public string Json { get; set; }
}

[MessagePackObject]
public class KEViJsonReplayData : ReplayData
{
	[Key(1)]
	public JsonType Type { get; set; }

	[Key(2)]
	public string Json { get; set; }

	public enum JsonType
	{
		Eew = 0,
	}
}

[MessagePackObject]
public class SNPLogEntryReplayData : ReplayData
{
	[Key(1)]
	public string Message { get; set; }
}

[MessagePackObject]
public class AxisJsonReplayData : ReplayData
{
	[Key(1)]
	public string Json { get; set; }
}
