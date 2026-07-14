using KyoshinEewViewer.Map.Layers;

namespace KyoshinEewViewer.Tests.Map;

/// <summary>
/// HoverTracker のユニットテスト。
/// </summary>
public class HoverTrackerTests
{
	private sealed record TestItem(int Id);

	private static HoverTracker<TestItem> CreateTracker()
		=> new((a, b) => a.Id == b.Id);

	[Fact(DisplayName = "SetHoveredの変化検知")]
	public void SetHoveredの変化検知()
	{
		var tracker = CreateTracker();

		// null → 1 は変化あり
		Assert.True(tracker.SetHovered(1));
		Assert.Equal(1, tracker.HoveredIndex);

		// 1 → 1 は変化なし
		Assert.False(tracker.SetHovered(1));
		Assert.Equal(1, tracker.HoveredIndex);

		// 1 → null は変化あり
		Assert.True(tracker.SetHovered(null));
		Assert.Null(tracker.HoveredIndex);
	}

	[Fact(DisplayName = "Rebindによる再解決")]
	public void Rebindによる再解決()
	{
		var tracker = CreateTracker();

		// (a) ホバー中の要素が新配列の別位置に移動した場合、インデックスが追従する
		var arrayA = new[] { new TestItem(10), new TestItem(20), new TestItem(30) };
		tracker.SetHovered(1); // arrayA[1] = Id20 をホバー中とする
		var arrayB = new[] { new TestItem(20), new TestItem(10), new TestItem(30) }; // Id20が先頭に移動
		tracker.Rebind(arrayA, arrayB);
		Assert.Equal(0, tracker.HoveredIndex);

		// (b) ホバー中の要素が新配列に存在しない場合、ホバーは解除される
		tracker.SetHovered(0); // arrayB[0] = Id20 をホバー中とする
		var arrayC = new[] { new TestItem(10), new TestItem(30) }; // Id20が存在しない
		tracker.Rebind(arrayB, arrayC);
		Assert.Null(tracker.HoveredIndex);

		// (c) oldSourceがnull、またはHoveredIndexが範囲外の場合はホバーを解除する
		tracker.SetHovered(5); // arrayCの範囲外(Length=2)
		tracker.Rebind(arrayC, arrayC);
		Assert.Null(tracker.HoveredIndex);

		tracker.SetHovered(0);
		tracker.Rebind(null, arrayC); // oldSourceがnull
		Assert.Null(tracker.HoveredIndex);

		// newSourceがnull、または空配列の場合もホバーを解除する
		tracker.SetHovered(0);
		tracker.Rebind(arrayC, null);
		Assert.Null(tracker.HoveredIndex);

		tracker.SetHovered(0);
		tracker.Rebind(arrayC, Array.Empty<TestItem>());
		Assert.Null(tracker.HoveredIndex);

		// (d) ホバーなし(null)の場合は何もしない
		tracker.SetHovered(null);
		tracker.Rebind(arrayA, arrayB);
		Assert.Null(tracker.HoveredIndex);
	}
}
