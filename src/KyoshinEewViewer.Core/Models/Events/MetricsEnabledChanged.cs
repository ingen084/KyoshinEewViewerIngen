namespace KyoshinEewViewer.Core.Models.Events;

/// <summary>
/// メトリクス収集の有効/無効が変更されたことを通知するイベント
/// </summary>
public class MetricsEnabledChanged
{
	public required bool IsEnabled { get; init; }
}
