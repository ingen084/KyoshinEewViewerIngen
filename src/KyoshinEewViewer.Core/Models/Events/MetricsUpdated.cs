using KyoshinEewViewer.Core.Models.Metrics;

namespace KyoshinEewViewer.Core.Models.Events;

/// <summary>
/// メトリクスが更新されたことを通知するイベント
/// </summary>
public class MetricsUpdated
{
	public required FrameRenderMetrics Metrics { get; init; }
}
