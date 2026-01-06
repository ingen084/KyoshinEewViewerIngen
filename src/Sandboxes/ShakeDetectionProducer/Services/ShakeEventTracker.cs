using KyoshinEewViewer.Core.Models;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShakeDetectionProducer.Services;

/// <summary>
/// イベントの変化理由
/// </summary>
[Flags]
public enum EventChangeReason
{
	None = 0,
	/// <summary>新規イベント</summary>
	NewEvent = 1 << 0,
	/// <summary>レベルアップ</summary>
	LevelUp = 1 << 1,
	/// <summary>レベルダウン</summary>
	LevelDown = 1 << 2,
	/// <summary>範囲が変化</summary>
	RegionChanged = 1 << 3,
	/// <summary>検出点が変化（追加・削除・点数変化）</summary>
	PointsChanged = 1 << 4,
}

/// <summary>
/// イベントの状態をキャッシュするための構造体
/// </summary>
internal record EventCacheEntry
{
	public required KyoshinEventLevel Level { get; init; }
	public required Location TopLeft { get; init; }
	public required Location BottomRight { get; init; }
	public required HashSet<string> PointCodes { get; init; }

	public static EventCacheEntry FromEvent(KyoshinEvent evt)
	{
		return new EventCacheEntry
		{
			Level = evt.Level,
			TopLeft = evt.TopLeft,
			BottomRight = evt.BottomRight,
			PointCodes = evt.Points.Select(p => p.Code).ToHashSet()
		};
	}
}

/// <summary>
/// 揺れイベントの変化を追跡するトラッカー
/// </summary>
public class ShakeEventTracker
{
	/// <summary>
	/// イベントIDとキャッシュエントリのマップ
	/// </summary>
	private Dictionary<Guid, EventCacheEntry> EventCache { get; } = [];

	/// <summary>
	/// イベントを処理して、送信が必要かどうかと変化理由を判定する
	/// </summary>
	/// <param name="evt">処理対象のイベント</param>
	/// <returns>
	/// ShouldSend: 送信が必要かどうか（何らかの変化があった場合）
	/// ChangeReason: 変化理由のフラグ
	/// </returns>
	public (bool ShouldSend, EventChangeReason ChangeReason) ProcessEvent(KyoshinEvent evt)
	{
		var newEntry = EventCacheEntry.FromEvent(evt);

		// 新規イベントの場合
		if (!EventCache.TryGetValue(evt.Id, out var cachedEntry))
		{
			EventCache[evt.Id] = newEntry;
			return (true, EventChangeReason.NewEvent);
		}

		var changeReason = EventChangeReason.None;

		// レベル変化の検出
		if (newEntry.Level > cachedEntry.Level)
			changeReason |= EventChangeReason.LevelUp;
		else if (newEntry.Level < cachedEntry.Level)
			changeReason |= EventChangeReason.LevelDown;

		// 範囲変化の検出（緯度経度が変化した場合）
		if (!IsLocationEqual(newEntry.TopLeft, cachedEntry.TopLeft) ||
		    !IsLocationEqual(newEntry.BottomRight, cachedEntry.BottomRight))
			changeReason |= EventChangeReason.RegionChanged;

		// 検出点の変化（追加・削除・点数変化）
		if (!newEntry.PointCodes.SetEquals(cachedEntry.PointCodes))
			changeReason |= EventChangeReason.PointsChanged;

		// キャッシュを更新
		EventCache[evt.Id] = newEntry;

		return (changeReason != EventChangeReason.None, changeReason);
	}

	/// <summary>
	/// 2つの座標が同一かどうかを判定
	/// </summary>
	private static bool IsLocationEqual(Location a, Location b)
	{
		const float tolerance = 0.0001f;
		return Math.Abs(a.Latitude - b.Latitude) < tolerance &&
		       Math.Abs(a.Longitude - b.Longitude) < tolerance;
	}

	/// <summary>
	/// 存在しなくなったイベントのキャッシュをクリーンアップする
	/// </summary>
	/// <param name="currentEvents">現在存在するイベントの配列</param>
	public void CleanupCache(KyoshinEvent[] currentEvents)
	{
		var currentEventIds = currentEvents.Select(e => e.Id).ToHashSet();

		foreach (var key in EventCache.Keys.ToArray())
		{
			if (!currentEventIds.Contains(key))
				EventCache.Remove(key);
		}
	}

	/// <summary>
	/// キャッシュをクリアする
	/// </summary>
	public void Clear()
	{
		EventCache.Clear();
	}
}
