using Avalonia.Platform;
using KyoshinEewViewer.TravelTimeTable;
using KyoshinEewViewer.TravelTimeTable.Models;
using MessagePack;
using System;
using System.Collections.Immutable;
using System.Linq;
using TravelTimeTableClass = KyoshinEewViewer.TravelTimeTable.TravelTimeTable;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

public static class TravelTimeTableService
{
	public static bool IsInitialized => TimeTable != null;
	private static TravelTimeTableItem[]? TimeTable { get; set; }

	/// <summary>
	/// 新しい走時表パッケージを使用したTravelTimeTable
	/// 震源探索エンジンで使用する
	/// </summary>
	public static TravelTimeTableClass? TravelTimeTableInstance { get; private set; }

	/// <summary>
	/// 震源探索エンジン
	/// </summary>
	public static HypocenterSearchEngine? HypocenterSearchEngine { get; private set; }

	public static (double? pDistance, double? sDistance) CalcDistance(DateTime occurranceTime, DateTime currentTime, int depth)
	{
		if (TimeTable == null)
			throw new InvalidOperationException("走時表の初期化が行われていません");
		if (!TimeTable.Any(t => t.Distance == depth))
			return (null, null);
		var elapsedTime = (currentTime - occurranceTime).TotalMilliseconds;
		if (elapsedTime <= 0)
			return (null, null);

		double? pDistance = null;
		double? sDistance = null;

		TravelTimeTableItem? lastItem = null;
		foreach (var item in TimeTable) // P
		{
			if (item.Depth != depth)
				continue;
			if (item.PTime > elapsedTime)
			{
				if (lastItem == null)
					break;
				// 時間での割合を計算
				var magn = (elapsedTime - lastItem.PTime) / (item.PTime - lastItem.PTime);
				pDistance = magn * (item.Distance - lastItem.Distance) + lastItem.Distance;
				break;
			}
			lastItem = item;
		}
		foreach (var item in TimeTable) // S
		{
			if (item.Depth != depth)
				continue;
			if (item.STime > elapsedTime)
			{
				if (lastItem == null)
					break;
				// 時間での割合を計算
				var magn = (elapsedTime - lastItem.STime) / (item.STime - lastItem.STime);
				sDistance = magn * (item.Distance - lastItem.Distance) + lastItem.Distance;
				break;
			}
			lastItem = item;
		}
		return (pDistance, sDistance);
	}

	public static void Initialize()
	{
		using var stream = AssetLoader.Open(new Uri("avares://KyoshinEewViewer/Assets/tjma2001.mpk.lz4", UriKind.Absolute)) ?? throw new Exception("走時表が読み込めません");
		TimeTable = MessagePackSerializer.Deserialize<ImmutableArray<TravelTimeTableItem>>(
			stream,
			MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray)
		).ToArray();

		// 新しいTravelTimeTableパッケージ用に変換
		var entries = TimeTable.Select(t => new TravelTimeEntry(
			distanceKm: t.Distance,
			depthKm: t.Depth,
			pTimeMs: t.PTime,
			sTimeMs: t.STime
		)).ToArray();

		TravelTimeTableInstance = TravelTimeTableClass.FromEntries(entries);
		HypocenterSearchEngine = new HypocenterSearchEngine(TravelTimeTableInstance);
	}
}

[MessagePackObject]
public class TravelTimeTableItem(int pTime, int sTime, int depth, int distance)
{
	[Key(0)]
	public int PTime { get; } = pTime;

	[Key(1)]
	public int STime { get; } = sTime;

	[Key(2)]
	public int Depth { get; } = depth;

	[Key(3)]
	public int Distance { get; } = distance;
}
