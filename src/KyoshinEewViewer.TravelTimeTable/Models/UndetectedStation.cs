using KyoshinMonitorLib;

namespace KyoshinEewViewer.TravelTimeTable.Models;

/// <summary>
/// 未検知観測点情報
/// まだ揺れを検知していない観測点の位置を保持する
/// </summary>
public readonly record struct UndetectedStation
{
    /// <summary>
    /// 観測点の位置
    /// </summary>
    public Location Location { get; init; }

    /// <summary>
    /// 観測点コード
    /// </summary>
    public string? Code { get; init; }

    public UndetectedStation(Location location, string? code = null)
    {
        Location = location;
        Code = code;
    }
}
