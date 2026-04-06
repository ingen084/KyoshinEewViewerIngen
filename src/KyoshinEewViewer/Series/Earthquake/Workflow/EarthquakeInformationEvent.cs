using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;
using System.ComponentModel;

namespace KyoshinEewViewer.Series.Earthquake.Workflow;

public class EarthquakeInformationEvent(EarthquakeSeries? series) : WorkflowEvent("EarthquakeInformation", series)
{
	[Description("情報の更新時刻")]
	public DateTime UpdatedAt { get; init; }

	[Description("最新の情報種別名 (例: \"震源・震度に関する情報\")")]
	public required string LatestInformationName { get; init; }

	[Description("イベントID P2P地震情報API経由の場合などはランダムな文字列になることがある")]
	public required string EarthquakeId { get; init; }

	[Description("訓練電文 / 試験電文かどうか")]
	public bool IsTrainingOrTest { get; init; }

	[Description("火山噴火に関連する情報かどうか")]
	public bool IsVolcano { get; init; }

	[Description("火山名 (火山関連情報の場合)")]
	public string? VolcanoName { get; init; }

	[Description("地震発生時刻 (震度速報の場合は検知時刻)")]
	public DateTime? DetectedAt { get; init; }

	[Description("最大震度 (Unknown, Int0, Int1, Int2, Int3, Int4, Int5Lower, Int5Upper, Int6Lower, Int6Upper, Int7)")]
	public JmaIntensity MaxIntensity { get; init; }

	[Description("最大震度の長い表記 (例: \"震度5強\")")]
	public string MaxIntensityLongName => MaxIntensity.ToLongString();

	[Description("更新前の最大震度 (Unknown, Int0, Int1, Int2, Int3, Int4, Int5Lower, Int5Upper, Int6Lower, Int6Upper, Int7) - 前回情報からの変化検出用")]
	public JmaIntensity? PreviousMaxIntensity { get; init; }

	[Description("最大長周期地震動階級 (Unknown, LpgmInt0, LpgmInt1, LpgmInt2, LpgmInt3, LpgmInt4, Error)")]
	public LpgmIntensity? MaxLpgmIntensity { get; init; }

	[Description("地震情報が取消されたかどうか")]
	public bool IsCancelled { get; init; }

	[Description("震源情報のみ (震度情報なし) の電文かどうか")]
	public bool IsHypocenterOnly { get; init; }

	[Description("詳細震度 (市区町村以下) が適用されているかどうか")]
	public bool IsDetailIntensityApplied { get; init; }

	[Description("震源情報")]
	public EarthquakeInformationEventHypocenter? Hypocenter { get; init; }

	// TODO: 実装したいがけっこう構造弄らないといけないかも
	// public List<ObservationIntensityGroup> Intensities { get; init; } = [];

	[Description("コメント")]
	public string? Comment { get; init; }

	[Description("自由形式コメント")]
	public string? FreeFormComment { get; init; }
}

public record EarthquakeInformationEventHypocenter(
	[property: Description("発生時刻")] DateTime OccurrenceAt,
	[property: Description("震源地名")] string? PlaceName,
	[property: Description("震源位置 (緯度経度)")] Location? Location,
	[property: Description("マグニチュード")] float Magnitude,
	[property: Description("マグニチュード代替テキスト (例: \"M7.0以上\")")] string? MagnitudeAlternativeText,
	[property: Description("震源の深さ (km)")] int Depth,
	[property: Description("深さのデータがない場合 true")] bool IsNoDepthData,
	[property: Description("ごく浅い震源かどうか")] bool IsVeryShallow,
	[property: Description("海外で発生した地震かどうか")] bool IsForeign
);

