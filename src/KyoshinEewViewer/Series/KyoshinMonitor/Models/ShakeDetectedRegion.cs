using KyoshinEewViewer.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// 揺れを検知した地域情報
/// </summary>
public class ShakeDetectedRegion
{
	/// <summary>
	/// 地域名
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// 全てのサブ地域が含まれているか（Region として表示可能か）
	/// </summary>
	public required bool IsFullRegion { get; init; }

	/// <summary>
	/// サブ地域名の配列（IsFullRegionがfalseの場合のみ有効）
	/// </summary>
	public string[]? SubRegions { get; init; }
}

/// <summary>
/// 揺れ検知地域をビルドするためのユーティリティ
/// </summary>
public static class ShakeDetectedRegionBuilder
{
	/// <summary>
	/// KyoshinEvent配列から揺れ検知地域を構築する
	/// 各イベントの最高レベルを検出した地域をまとめて表示
	/// </summary>
	/// <param name="events">強震イベント配列</param>
	/// <param name="regionSubRegionMap">Region → SubRegion[] のマッピング（全観測点から構築）</param>
	/// <returns>揺れ検知地域の配列</returns>
	public static ShakeDetectedRegion[] Build(
		KyoshinEvent[] events,
		Dictionary<string, HashSet<string?>> regionSubRegionMap)
	{
		if (events.Length == 0)
			return [];

		// 最大レベルを取得
		var maxLevel = events.Max(e => e.Level);

		// 最大レベルのイベントの最高レベル地域のみを集約
		var allRegions = events
			.Where(e => e.Level == maxLevel)
			.SelectMany(e => e.PeakLevelRegions)
			.ToHashSet();

		if (allRegions.Count == 0)
			return [];

		// Region ごとにグループ化
		var regionGroups = allRegions
			.GroupBy(r => r.Region)
			.OrderBy(g => g.Key)
			.ToList();

		var results = new List<ShakeDetectedRegion>();

		foreach (var regionGroup in regionGroups)
		{
			var regionName = regionGroup.Key;
			var detectedSubRegions = regionGroup
				.Select(r => r.SubRegion)
				.Where(s => s != null)
				.Cast<string>()
				.Distinct()
				.OrderBy(s => s)
				.ToArray();

			// 全サブ地域を含んでいるかチェック
			var isFullRegion = false;
			if (regionSubRegionMap.TryGetValue(regionName, out var allSubRegions))
			{
				// SubRegion が null のみの場合（サブ地域がない Region）
				if (allSubRegions.All(s => s == null))
				{
					isFullRegion = true;
				}
				else
				{
					// null 以外の全サブ地域が含まれているか
					var nonNullSubRegions = allSubRegions.Where(s => s != null).ToHashSet();
					isFullRegion = nonNullSubRegions.Count > 0 &&
								   nonNullSubRegions.All(s => detectedSubRegions.Contains(s));
				}
			}

			results.Add(new ShakeDetectedRegion
			{
				Name = regionName,
				IsFullRegion = isFullRegion,
				SubRegions = detectedSubRegions,
			});
		}

		return [.. results];
	}
}
