using MessagePack;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;

public class ObservationPointsFileReader(Stream stream) : IDisposable
{
    private static ReadOnlySpan<byte> HeaderMagic => "KMOP"u8;

    public async Task<ObservationPointsFileHeader> ReadHeader()
    {
        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[HeaderMagic.Length];
        if ((await stream.ReadAsync(buffer.AsMemory())) < HeaderMagic.Length || !HeaderMagic.SequenceEqual(buffer))
            throw new InvalidDataException("Magic Header の読み込みができませんでした");

        var header = MessagePackSerializer.Deserialize<ObservationPointsFileHeader>(stream);
        if (header.Version != 0)
            throw new InvalidDataException($"未対応のバージョンです({header.Version})");
        return header;
    }

    public async Task<ObservationPointV2[]> ReadData(ObservationPointsCompressionMode compressionMode)
    {
        var option = MessagePackSerializerOptions.Standard;
        switch (compressionMode)
        {
            case ObservationPointsCompressionMode.GZip:
                using (var gzip = new GZipStream(stream, CompressionMode.Decompress, true))
                    return await MessagePackSerializer.DeserializeAsync<ObservationPointV2[]>(gzip, option);
            case ObservationPointsCompressionMode.Brotli:
                using (var brotli = new BrotliStream(stream, CompressionMode.Decompress, true))
                    return await MessagePackSerializer.DeserializeAsync<ObservationPointV2[]>(brotli, option);
            default:
                if (compressionMode == ObservationPointsCompressionMode.MessagePackCSharpLz4BlockArray)
                    option = option.WithCompression(MessagePackCompression.Lz4BlockArray);
                return await MessagePackSerializer.DeserializeAsync<ObservationPointV2[]>(stream, option);
        }
    }

    public async Task WriteHeader(ObservationPointsFileHeader header)
    {
        stream.Seek(0, SeekOrigin.Begin);
        await stream.WriteAsync(HeaderMagic.ToArray());
        await MessagePackSerializer.SerializeAsync(stream, header);
    }

    public async Task WriteData(ObservationPointV2[] data, ObservationPointsCompressionMode compressionMode)
    {
        var option = MessagePackSerializerOptions.Standard;
        switch (compressionMode)
        {
            case ObservationPointsCompressionMode.GZip:
                using (var gzip = new GZipStream(stream, CompressionLevel.SmallestSize, true))
                    await MessagePackSerializer.SerializeAsync(gzip, data, option);
                break;
            case ObservationPointsCompressionMode.Brotli:
                using (var brotli = new BrotliStream(stream, CompressionLevel.SmallestSize, true))
                    await MessagePackSerializer.SerializeAsync(brotli, data, option);
                break;
            default:
                if (compressionMode == ObservationPointsCompressionMode.MessagePackCSharpLz4BlockArray)
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
public class ObservationPointsFileHeader
{
    [Key(0)]
    public uint Version { get; set; } = 0;

    [Key(1)]
    public string DataVersion { get; set; } = "";

    [Key(2)]
    public DateTime PackedAt { get; set; }

    [Key(3)]
    public string Source { get; set; } = "";

    [Key(4)]
    public ObservationPointsCompressionMode CompressionMode { get; set; }
}

public enum ObservationPointsCompressionMode
{
    None = 0,
    MessagePackCSharpLz4BlockArray = 1,
    GZip = 2,
    Brotli = 3,
}
