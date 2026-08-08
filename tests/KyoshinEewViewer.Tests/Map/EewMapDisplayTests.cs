using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;
using SkiaSharp;

namespace KyoshinEewViewer.Tests.Map;

public class EewMapDisplayTests
{
	[Fact(DisplayName = "継続中と最終報のラベルを指定形式で組み立てる")]
	public void 継続中と最終報のラベルを指定形式で組み立てる()
	{
		var ongoing = CreateEew(serialNo: 1, place: "岩手県沖", magnitude: 3.7f, intensity: JmaIntensity.Int1);
		var final = CreateEew(serialNo: 4, place: "岩手県沖", magnitude: 3.5f, intensity: JmaIntensity.Int1, isFinal: true);

		Assert.Equal("#1 岩手県沖 M3.7 最大震度1", EewMapDisplay.BuildLabel(ongoing));
		Assert.Equal("#4(終) 岩手県沖 M3.5 最大震度1", EewMapDisplay.BuildLabel(final));
	}

	[Fact(DisplayName = "確定と未確定のキャンセル状態をラベルで区別する")]
	public void 確定と未確定のキャンセル状態をラベルで区別する()
	{
		var cancelled = CreateEew(serialNo: 3, isFinal: true, isCancelled: true, isTrueCancelled: true);
		var possiblyCancelled = CreateEew(serialNo: 4, isCancelled: true);

		Assert.StartsWith("#3(取消) ", EewMapDisplay.BuildLabel(cancelled));
		Assert.StartsWith("#4(取消?) ", EewMapDisplay.BuildLabel(possiblyCancelled));
	}

	[Fact(DisplayName = "未取得の震源要素を不明として表示する")]
	public void 未取得の震源要素を不明として表示する()
	{
		var eew = CreateEew(serialNo: 2, place: null, magnitude: null, intensity: JmaIntensity.Unknown);

		Assert.Equal("#2 震源地不明 M不明 最大震度不明", EewMapDisplay.BuildLabel(eew));
	}

	[Fact(DisplayName = "最大震度の以上表現をラベルへ反映する")]
	public void 最大震度の以上表現をラベルへ反映する()
	{
		var eew = CreateEew(serialNo: 2, intensity: JmaIntensity.Int5Lower, isIntensityOver: true);

		Assert.EndsWith("最大震度5弱以上", EewMapDisplay.BuildLabel(eew));
	}

	[Fact(DisplayName = "継続中と最終報が混在する場合だけ最終報を減光する")]
	public void 継続中と最終報が混在する場合だけ最終報を減光する()
	{
		var ongoing = CreateEew(serialNo: 1);
		var final1 = CreateEew(serialNo: 2, isFinal: true);
		var final2 = CreateEew(serialNo: 3, isFinal: true);

		Assert.True(EewMapDisplay.ShouldDimFinalEews([ongoing, final1]));
		Assert.Equal(byte.MaxValue, EewMapDisplay.GetContentOpacity(ongoing, dimFinalEews: true));
		Assert.Equal(EewMapDisplay.FinalContentOpacity, EewMapDisplay.GetContentOpacity(final1, dimFinalEews: true));
		Assert.Equal(EewMapDisplay.InactiveLabelOpacity, EewMapDisplay.GetLabelOpacity(final1, dimFinalEews: true));

		Assert.False(EewMapDisplay.ShouldDimFinalEews([final1, final2]));
		Assert.Equal(byte.MaxValue, EewMapDisplay.GetContentOpacity(final1, dimFinalEews: false));
		Assert.Equal(byte.MaxValue, EewMapDisplay.GetLabelOpacity(final1, dimFinalEews: false));
	}

	[Fact(DisplayName = "ラベルはEEWが複数ある場合だけ表示する")]
	public void ラベルはEEWが複数ある場合だけ表示する()
	{
		var eew = CreateEew(serialNo: 1);

		Assert.False(EewMapDisplay.ShouldShowLabels([eew]));
		Assert.True(EewMapDisplay.ShouldShowLabels([eew, CreateEew(serialNo: 2)]));
	}

	[Fact(DisplayName = "密集したEEWラベルを重ねず継続中から優先配置する")]
	public void 密集したEEWラベルを重ねず継続中から優先配置する()
	{
		var center = new SKPoint(500, 300);
		var items = new EewMapDisplay.LabelItem[]
		{
			new(0, CreateEew(serialNo: 4, isFinal: true), center, 10),
			new(1, CreateEew(serialNo: 1), center, 10),
			new(2, CreateEew(serialNo: 5, isCancelled: true, isTrueCancelled: true), center, 10),
			new(3, CreateEew(serialNo: 6, isFinal: true), center, 10),
			new(4, CreateEew(serialNo: 7, isCancelled: true), center, 10),
		};
		var viewport = SKRect.Create(0, 0, 1000, 600);

		var placements = EewMapDisplay.BuildLabelLayout(
			items,
			viewport,
			dimFinalEews: true,
			measureLines: _ => (220, 23));

		Assert.All(placements, placement => Assert.NotNull(placement));
		Assert.True(placements[1]!.Value.Rect.Left > center.X); // 継続中が既定位置（震源の右）を確保する

		var markerRect = SKRect.Create(center.X - 12, center.Y - 12, 24, 24);
		for (var i = 0; i < placements.Length; i++)
		{
			var current = placements[i]!.Value.Rect;
			Assert.False(current.IntersectsWith(markerRect));
			Assert.True(current.Left >= viewport.Left && current.Top >= viewport.Top);
			Assert.True(current.Right <= viewport.Right && current.Bottom <= viewport.Bottom);
			for (var j = i + 1; j < placements.Length; j++)
				Assert.False(current.IntersectsWith(placements[j]!.Value.Rect));
		}
	}

	private static Eew CreateEew(
		int serialNo,
		string? place = "テスト震源",
		float? magnitude = 4.2f,
		JmaIntensity intensity = JmaIntensity.Int3,
		bool isIntensityOver = false,
		bool isFinal = false,
		bool isCancelled = false,
		bool isTrueCancelled = false)
		=> new()
		{
			Id = $"test-{serialNo}",
			Source = EewSource.Dmdata,
			DisplaySource = "テスト",
			IsCancelled = isCancelled,
			IsTrueCancelled = isTrueCancelled,
			ReceiveTime = new DateTime(2026, 8, 8, 12, 0, serialNo, DateTimeKind.Local),
			SerialNo = serialNo,
			IsFinal = isFinal,
			MaxIntensity = intensity,
			IsIntensityOver = isIntensityOver,
			Hypocenter = new EewHypocenter
			{
				OccurrenceTime = new DateTime(2026, 8, 8, 11, 59, serialNo, DateTimeKind.Local),
				Place = place,
				Location = new Location(39, 142),
				Magnitude = magnitude,
				Depth = 20,
				IsTemporary = false,
			},
			IsWarning = false,
		};
}
