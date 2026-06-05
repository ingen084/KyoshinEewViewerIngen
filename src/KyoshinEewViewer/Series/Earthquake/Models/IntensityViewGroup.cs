using KyoshinEewViewer.Core;
using KyoshinMonitorLib;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 震度別にグルーピングされた観測情報ツリー (View 表示用)
/// </summary>
public sealed record IntensityViewGroup(
	JmaIntensity Intensity,
	IReadOnlyList<PrefectureIntensitySnapshot> Prefectures);

/// <summary>
/// 長周期地震動階級別にグルーピングされた観測情報ツリー (View 表示用)
/// </summary>
public sealed record LpgmIntensityViewGroup(
	LpgmIntensity Intensity,
	IReadOnlyList<PrefectureIntensitySnapshot> Prefectures);
