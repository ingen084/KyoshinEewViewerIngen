using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

/// <summary>
/// 強震イベントの状態追跡と更新判定を行うヘルパークラス
/// </summary>
public class KyoshinEventStateTracker
{
	private Dictionary<Guid, KyoshinEventLevel> LevelCache { get; } = [];
	private Dictionary<Guid, int> RegionCountCache { get; } = [];
	private Dictionary<Guid, int> SubRegionCountCache { get; } = [];

	/// <summary>
	/// イベント更新判定結果
	/// </summary>
	public readonly record struct UpdateResult(
		bool IsNewEvent,
		bool IsLevelUp,
		bool IsRegionExpanded,
		bool IsSubRegionExpanded
	)
	{
		/// <summary>
		/// 通知が必要かどうか
		/// </summary>
		public bool ShouldNotify => IsNewEvent || IsLevelUp || IsRegionExpanded || IsSubRegionExpanded;
	}

	/// <summary>
	/// イベントの状態を確認し、キャッシュを更新する
	/// </summary>
	public UpdateResult CheckAndUpdate(KyoshinEvent evt)
	{
		var cachedLevel = LevelCache.TryGetValue(evt.Id, out var lv) ? lv : (KyoshinEventLevel?)null;
		var isNewEvent = cachedLevel == null;
		var isLevelUp = cachedLevel != null && cachedLevel < evt.Level;

		// 地域拡大判定（地域数とサブ地域数を別々に判定）
		var currentRegionCount = evt.PeakLevelRegions.Select(r => r.Region).Distinct().Count();
		var currentSubRegionCount = evt.PeakLevelRegions.Count;
		var cachedRegionCount = RegionCountCache.TryGetValue(evt.Id, out var rc) ? rc : 0;
		var cachedSubRegionCount = SubRegionCountCache.TryGetValue(evt.Id, out var src) ? src : 0;
		var isRegionExpanded = !isNewEvent && !isLevelUp && currentRegionCount > cachedRegionCount;
		var isSubRegionExpanded = !isNewEvent && !isLevelUp && !isRegionExpanded && currentSubRegionCount > cachedSubRegionCount;

		// キャッシュを更新
		LevelCache[evt.Id] = evt.Level;
		RegionCountCache[evt.Id] = currentRegionCount;
		SubRegionCountCache[evt.Id] = currentSubRegionCount;

		return new UpdateResult(isNewEvent, isLevelUp, isRegionExpanded, isSubRegionExpanded);
	}

	/// <summary>
	/// 存在しないイベントに対するキャッシュを削除する
	/// </summary>
	public void RemoveStaleEntries(KyoshinEvent[] currentEvents)
	{
		var currentIds = currentEvents.Select(e => e.Id).ToHashSet();

		foreach (var key in LevelCache.Keys.ToArray())
			if (!currentIds.Contains(key))
				LevelCache.Remove(key);

		foreach (var key in RegionCountCache.Keys.ToArray())
			if (!currentIds.Contains(key))
				RegionCountCache.Remove(key);

		foreach (var key in SubRegionCountCache.Keys.ToArray())
			if (!currentIds.Contains(key))
				SubRegionCountCache.Remove(key);
	}

	/// <summary>
	/// すべてのキャッシュをクリアする
	/// </summary>
	public void Clear()
	{
		LevelCache.Clear();
		RegionCountCache.Clear();
		SubRegionCountCache.Clear();
	}
}
