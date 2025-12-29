using KyoshinEewViewer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShakeDetectionProducer.Services;

/// <summary>
/// 揺れイベントのレベルアップ判定を行うトラッカー
/// </summary>
public class ShakeEventTracker
{
	/// <summary>
	/// イベントIDとレベルのキャッシュ
	/// </summary>
	private Dictionary<Guid, KyoshinEventLevel> EventLevelCache { get; } = [];

	/// <summary>
	/// イベントを処理して、Kafka送信が必要かどうかを判定する
	/// </summary>
	/// <param name="evt">処理対象のイベント</param>
	/// <returns>
	/// ShouldSend: Kafka送信が必要かどうか（新規イベントまたはレベルアップ時）
	/// IsLevelUp: レベルアップしたかどうか
	/// </returns>
	public (bool ShouldSend, bool IsLevelUp) ProcessEvent(KyoshinEvent evt)
	{
		// 既存のイベントでレベル上昇なしの場合は送信不要
		if (EventLevelCache.TryGetValue(evt.Id, out var cachedLevel) && cachedLevel >= evt.Level)
		{
			// キャッシュを更新（レベルダウンの場合も追跡）
			EventLevelCache[evt.Id] = evt.Level;
			return (false, false);
		}

		// 新規イベントまたはレベルアップ
		var isLevelUp = EventLevelCache.ContainsKey(evt.Id);
		EventLevelCache[evt.Id] = evt.Level;

		return (true, isLevelUp);
	}

	/// <summary>
	/// 存在しなくなったイベントのキャッシュをクリーンアップする
	/// </summary>
	/// <param name="currentEvents">現在存在するイベントの配列</param>
	public void CleanupCache(KyoshinEvent[] currentEvents)
	{
		var currentEventIds = currentEvents.Select(e => e.Id).ToHashSet();

		foreach (var key in EventLevelCache.Keys.ToArray())
		{
			if (!currentEventIds.Contains(key))
				EventLevelCache.Remove(key);
		}
	}

	/// <summary>
	/// キャッシュをクリアする
	/// </summary>
	public void Clear()
	{
		EventLevelCache.Clear();
	}
}
