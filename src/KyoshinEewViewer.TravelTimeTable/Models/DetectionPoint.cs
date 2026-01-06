using KyoshinMonitorLib;

namespace KyoshinEewViewer.TravelTimeTable.Models;

/// <summary>
/// 検知観測点情報
/// 揺れを検知した観測点の位置と検知時刻を保持する
/// </summary>
public readonly record struct DetectionPoint
{
    /// <summary>
    /// 観測点の位置
    /// </summary>
    public Location Location { get; init; }

    /// <summary>
    /// 揺れ検知時刻
    /// </summary>
    public DateTime DetectedAt { get; init; }

    /// <summary>
    /// 観測点コード
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// 検知時の震度
    /// </summary>
    public double? Intensity { get; init; }

    public DetectionPoint(Location location, DateTime detectedAt, string? code = null, double? intensity = null)
    {
        Location = location;
        DetectedAt = detectedAt;
        Code = code;
        Intensity = intensity;
    }
}
