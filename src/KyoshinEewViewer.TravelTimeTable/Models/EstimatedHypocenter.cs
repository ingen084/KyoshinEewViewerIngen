using KyoshinMonitorLib;

namespace KyoshinEewViewer.TravelTimeTable.Models;

/// <summary>
/// 推定震源要素
/// 揺れ検知から推定された震源情報を保持する
/// </summary>
public record class EstimatedHypocenter
{
    /// <summary>
    /// 推定震央位置
    /// </summary>
    public required Location Location { get; init; }

    /// <summary>
    /// 推定震源深さ (km)
    /// </summary>
    public required int DepthKm { get; init; }

    /// <summary>
    /// 推定発震時刻
    /// </summary>
    public required DateTime OriginTime { get; init; }

    /// <summary>
    /// 推定の信頼度スコア（0.0-1.0、高いほど信頼度が高い）
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// 推定に使用した観測点数
    /// </summary>
    public required int UsedStationCount { get; init; }

    /// <summary>
    /// 残差の標準偏差（秒）
    /// </summary>
    public required double ResidualStdDev { get; init; }

    /// <summary>
    /// 推定に使用したアルゴリズムのバージョン
    /// </summary>
    public int AlgorithmVersion { get; init; } = 1;

    /// <summary>
    /// 最終更新時刻
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
