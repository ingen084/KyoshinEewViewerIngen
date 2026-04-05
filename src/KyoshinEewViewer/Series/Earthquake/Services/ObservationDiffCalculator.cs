using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 観測地域の差分計算の実装
/// </summary>
public class ObservationDiffCalculator : IObservationDiffCalculator
{
	/// <summary>
	/// 階層構造の観測情報から差分を計算する
	/// 都道府県→細分区域をフラット化し、地域コードをキーにして比較する
	/// </summary>
	public ObservationDiff ComputeFromPrefs(
		EarthquakeObservationPref[]? previous,
		EarthquakeObservationPref[]? current)
	{
		var prevAreas = FlattenAreas(previous);
		var currAreas = FlattenAreas(current);

		return ComputeDiff(prevAreas, currAreas);
	}

	/// <summary>
	/// フラット構造の観測情報から差分を計算する（P2P地震情報用）
	/// Addressをキーにして比較する
	/// </summary>
	public ObservationDiff ComputeFromFlatPoints(
		EarthquakeObservationFlatPoint[]? previous,
		EarthquakeObservationFlatPoint[]? current)
	{
		var prevAreas = FlattenFlatPoints(previous);
		var currAreas = FlattenFlatPoints(current);

		return ComputeDiff(prevAreas, currAreas);
	}

	private static Dictionary<int, (string Name, JmaIntensity Intensity)> FlattenAreas(EarthquakeObservationPref[]? prefs)
	{
		var result = new Dictionary<int, (string Name, JmaIntensity Intensity)>();
		if (prefs == null)
			return result;

		foreach (var pref in prefs)
		{
			foreach (var area in pref.Areas)
				result.TryAdd(area.Code, (area.Name, area.MaxIntensity));
		}
		return result;
	}

	private static Dictionary<string, (string Name, JmaIntensity Intensity)> FlattenFlatPoints(EarthquakeObservationFlatPoint[]? points)
	{
		var result = new Dictionary<string, (string Name, JmaIntensity Intensity)>();
		if (points == null)
			return result;

		foreach (var point in points)
			result.TryAdd(point.Address, (point.Address, point.Intensity));
		return result;
	}

	private static ObservationDiff ComputeDiff<TKey>(
		Dictionary<TKey, (string Name, JmaIntensity Intensity)> prevAreas,
		Dictionary<TKey, (string Name, JmaIntensity Intensity)> currAreas)
		where TKey : notnull
	{
		if (prevAreas.Count == 0 && currAreas.Count == 0)
			return ObservationDiff.Empty;

		var added = new List<ObservationAreaChange>();
		var removed = new List<ObservationAreaChange>();
		var intensityChanged = new List<ObservationAreaIntensityChange>();

		// 今回にあって前回にない → 追加、両方にある → 震度変化チェック
		foreach (var (key, (name, intensity)) in currAreas)
		{
			var code = key is int i ? i : 0;
			if (!prevAreas.TryGetValue(key, out var prev))
			{
				added.Add(new ObservationAreaChange(code, name, intensity));
			}
			else if (prev.Intensity != intensity)
			{
				intensityChanged.Add(new ObservationAreaIntensityChange(code, name, prev.Intensity, intensity));
			}
		}

		// 前回にあって今回にない → 削除
		foreach (var (key, (name, intensity)) in prevAreas)
		{
			var code = key is int i ? i : 0;
			if (!currAreas.ContainsKey(key))
				removed.Add(new ObservationAreaChange(code, name, intensity));
		}

		if (added.Count == 0 && removed.Count == 0 && intensityChanged.Count == 0)
			return ObservationDiff.Empty;

		return new ObservationDiff
		{
			AddedAreas = added.ToArray(),
			RemovedAreas = removed.ToArray(),
			IntensityChangedAreas = intensityChanged.ToArray(),
		};
	}
}
