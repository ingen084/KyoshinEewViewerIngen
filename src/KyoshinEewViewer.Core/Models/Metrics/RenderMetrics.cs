using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Core.Models.Metrics;

/// <summary>
/// 個別レイヤーの描画メトリクス
/// </summary>
public class LayerRenderMetrics
{
	/// <summary>
	/// レイヤー名
	/// </summary>
	public required string LayerName { get; init; }

	/// <summary>
	/// 描画処理時間
	/// </summary>
	public required TimeSpan RenderTime { get; init; }

	/// <summary>
	/// 描画内容情報（KeyValueペア）
	/// 例: { {"EEW件数", 3}, {"地震情報件数", 5} }
	/// </summary>
	public IReadOnlyDictionary<string, object?>? RenderInfo { get; init; }

	/// <summary>
	/// メトリクス取得時刻
	/// </summary>
	public required DateTime Timestamp { get; init; }
}

/// <summary>
/// フレーム全体の描画メトリクス
/// </summary>
public class FrameRenderMetrics
{
	/// <summary>
	/// フレーム全体の処理時間
	/// </summary>
	public required TimeSpan TotalFrameTime { get; init; }

	/// <summary>
	/// 各レイヤーのメトリクス
	/// </summary>
	public required IReadOnlyList<LayerRenderMetrics> LayerMetrics { get; init; }

	/// <summary>
	/// メトリクス取得時刻
	/// </summary>
	public required DateTime Timestamp { get; init; }

	/// <summary>
	/// ナビゲーション中かどうか
	/// </summary>
	public bool IsNavigating { get; init; }

	/// <summary>
	/// ズームレベル
	/// </summary>
	public double Zoom { get; init; }

	/// <summary>
	/// 左上の位置（緯度経度）
	/// </summary>
	public PointD LeftTopLocation { get; init; }

	/// <summary>
	/// 左上のピクセル座標
	/// </summary>
	public PointD LeftTopPixel { get; init; }

	/// <summary>
	/// ピクセル境界
	/// </summary>
	public RectD PixelBound { get; init; }

	/// <summary>
	/// 表示エリアの矩形
	/// </summary>
	public RectD ViewAreaRect { get; init; }
}
