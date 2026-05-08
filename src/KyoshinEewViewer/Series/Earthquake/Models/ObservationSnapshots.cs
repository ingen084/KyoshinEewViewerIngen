using KyoshinEewViewer.Core;
using KyoshinMonitorLib;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 都道府県単位の観測情報
/// </summary>
public sealed record PrefectureIntensitySnapshot(
	string Name,
	int Code,
	JmaIntensity MaxIntensity,
	LpgmIntensity? MaxLpgmIntensity,
	IReadOnlyList<AreaIntensitySnapshot> Areas);

/// <summary>
/// 細分区域単位の観測情報
/// </summary>
public sealed record AreaIntensitySnapshot(
	string Name,
	int Code,
	JmaIntensity MaxIntensity,
	LpgmIntensity? MaxLpgmIntensity,
	Location? CenterLocation,
	IReadOnlyList<CityIntensitySnapshot> Cities);

/// <summary>
/// 市区町村単位の観測情報
/// </summary>
public sealed record CityIntensitySnapshot(
	string Name,
	int Code,
	JmaIntensity MaxIntensity,
	LpgmIntensity? MaxLpgmIntensity,
	Location? CenterLocation,
	IReadOnlyList<StationIntensitySnapshot> Stations);

/// <summary>
/// 観測点単位の観測情報
/// </summary>
public sealed record StationIntensitySnapshot(
	string Name,
	int Code,
	JmaIntensity Intensity,
	LpgmIntensity? MaxLpgmIntensity,
	IReadOnlyList<LgIntByPeriod>? LpgmByPeriod,
	Location? Location);

/// <summary>
/// 周期帯ごとの長周期地震動階級
/// </summary>
public sealed record LgIntByPeriod(int PeriodicBand, LpgmIntensity Intensity);
