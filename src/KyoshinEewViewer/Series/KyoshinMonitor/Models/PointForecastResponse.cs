using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// Webhook のレスポンスとして受け取る地点予測
/// </summary>
public class PointForecastResponse
{
	/// <summary>
	/// 提供元の識別子 省略時はワークフローのIDを使用する
	/// </summary>
	public string? Source { get; set; }

	/// <summary>
	/// 表示に使用する提供元名 省略時はワークフロー名を使用する
	/// </summary>
	public string? DisplaySource { get; set; }

	/// <summary>
	/// 地点予測の一覧 空配列の場合はその提供元の地点予測をすべて削除する
	/// </summary>
	public PointForecastResponsePoint[]? Points { get; set; }
}

public class PointForecastResponsePoint
{
	/// <summary>
	/// 地点名
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// 緯度
	/// </summary>
	public double? Lat { get; set; }

	/// <summary>
	/// 経度
	/// </summary>
	public double? Lon { get; set; }

	/// <summary>
	/// 計測震度
	/// </summary>
	public double? Intensity { get; set; }

	/// <summary>
	/// 揺れの到達時刻 タイムゾーンオフセットがない場合は日本標準時として扱う
	/// </summary>
	public string? ArrivalAt { get; set; }
}

// トリミング時にプロパティが削除されないよう、ソース生成のコンテキストを使用する
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PointForecastResponse))]
public partial class PointForecastSerializerContext : JsonSerializerContext;
