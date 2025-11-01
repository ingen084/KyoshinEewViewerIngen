using KyoshinMonitorLib;
using MessagePack;
using MessagePack.Formatters;
using System;

namespace KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint.Formatter;

/// <summary>
/// Locationのカスタムフォーマッター（容量削減版）
/// </summary>
public class LocationFormatter : IMessagePackFormatter<Location?>
{
    public void Serialize(ref MessagePackWriter writer, Location? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        // 配列形式で保存
        writer.WriteArrayHeader(2);

        // 緯度・経度を10^4倍してint32で保存
        // 精度: 0.001度 ≈ 100メートル
        writer.Write((int)Math.Round(value.Latitude * 1000));
        writer.Write((int)Math.Round(value.Longitude * 1000));
    }

    public Location? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);

        if (reader.TryReadNil())
        {
            reader.Depth--;
            return null;
        }

        var count = reader.ReadArrayHeader();
        if (count < 2)
            throw new MessagePackSerializationException("Invalid Location format");

        var latInt = reader.ReadInt32();
        var lngInt = reader.ReadInt32();

        // 将来フィールドが追加された場合、残りをスキップ
        for (int i = 2; i < count; i++)
            reader.Skip();

        reader.Depth--;

        return new Location(
            latInt / 1000f,
            lngInt / 1000f
        );
    }
}
