using KyoshinEewViewer.Core;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// ある時点での地震情報の集約状態
/// </summary>
public sealed record EarthquakeSnapshot(
	EarthquakeIntensityScope Scope,
	EarthquakeHypocenterSnapshot? Hypocenter,
	JmaIntensity MaxIntensity,
	LpgmIntensity? MaxLpgmIntensity,
	DateTime? DetectionTime,
	string? SokuhouPlace,
	bool IsSingleArea,
	string? Comment,
	string? FreeFormComment,
	DateTime UpdatedTime,
	IReadOnlyList<PrefectureIntensitySnapshot> Prefectures);

/// <summary>
/// 震源要素のスナップショット (値直接保持)
/// </summary>
public sealed record EarthquakeHypocenterSnapshot(
	DateTime OriginTime,
	string Place,
	Location Location,
	Location? LocationError,
	float Magnitude,
	string? MagnitudeAlternativeText,
	int Depth,
	int? DepthError,
	bool IsForeign,
	bool IsVolcano,
	string? VolcanoName);

/// <summary>
/// 観測情報のスコープ
/// </summary>
public enum EarthquakeIntensityScope
{
	/// <summary>震源情報のみ</summary>
	HypocenterOnly,
	/// <summary>震度速報 (細分区域単位)</summary>
	AreaOnly,
	/// <summary>震源・震度に関する情報</summary>
	Detail,
	/// <summary>長周期地震動に関する観測情報</summary>
	Lpgm,
}
