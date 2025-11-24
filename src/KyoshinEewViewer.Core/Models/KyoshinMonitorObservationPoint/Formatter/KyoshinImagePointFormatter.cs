using KyoshinMonitorLib;
using MessagePack;
using MessagePack.Formatters;
using System;

namespace KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint.Formatter;

public class KyoshinImagePointFormatter : IMessagePackFormatter<KyoshinImagePoint?>
{
    public KyoshinImagePoint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        if (reader.TryReadNil())
        {
            reader.Depth--;
            return null;
        }

        var count = reader.ReadArrayHeader();
        if (count < 2)
            throw new InvalidOperationException("Invalid array length");

        var center = options.Resolver.GetFormatterWithVerify<Point2>().Deserialize(ref reader, options);
        var rawOffset = options.Resolver.GetFormatterWithVerify<byte>().Deserialize(ref reader, options);

        // 将来フィールドが追加された場合、残りをスキップ
        for (int i = 2; i < count; i++)
            reader.Skip();
            
        reader.Depth--;
        return new KyoshinImagePoint(center, new Point2(((rawOffset >> 4) & 0x0F) - 8, (rawOffset & 0x0F) - 8));
    }

    public void Serialize(ref MessagePackWriter writer, KyoshinImagePoint? value, MessagePackSerializerOptions options)
    {
        if (value is not { } v)
        {
            writer.WriteNil();
            return;
        }
        
        writer.WriteArrayHeader(2);
        options.Resolver.GetFormatterWithVerify<Point2>().Serialize(ref writer, v.Center, options);
        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref writer, (byte)((((v.Offset.X + 8) & 0x0F) << 4) | ((v.Offset.Y + 8) & 0x0F)), options);
    }
}
