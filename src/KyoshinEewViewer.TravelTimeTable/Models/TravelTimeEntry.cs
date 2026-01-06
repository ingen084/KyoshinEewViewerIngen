using MessagePack;

namespace KyoshinEewViewer.TravelTimeTable.Models;

/// <summary>
/// 走時表エントリ
/// 特定の深さ・距離における P波/S波の走時を保持する
/// </summary>
[MessagePackObject]
public readonly record struct TravelTimeEntry
{
    /// <summary>
    /// 震央距離 (km)
    /// </summary>
    [Key(0)]
    public int DistanceKm { get; init; }

    /// <summary>
    /// 震源深さ (km)
    /// </summary>
    [Key(1)]
    public int DepthKm { get; init; }

    /// <summary>
    /// P波走時 (ミリ秒)
    /// </summary>
    [Key(2)]
    public int PTimeMs { get; init; }

    /// <summary>
    /// S波走時 (ミリ秒)
    /// </summary>
    [Key(3)]
    public int STimeMs { get; init; }

    /// <summary>
    /// P波走時 (秒)
    /// </summary>
    [IgnoreMember]
    public double PTimeSeconds => PTimeMs / 1000.0;

    /// <summary>
    /// S波走時 (秒)
    /// </summary>
    [IgnoreMember]
    public double STimeSeconds => STimeMs / 1000.0;

    public TravelTimeEntry(int distanceKm, int depthKm, int pTimeMs, int sTimeMs)
    {
        DistanceKm = distanceKm;
        DepthKm = depthKm;
        PTimeMs = pTimeMs;
        STimeMs = sTimeMs;
    }
}
