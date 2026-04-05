using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Services.Eew;

/// <summary>
/// EewControllerの基本操作、ShowDetailsフィルタ、状態遷移のテスト
/// </summary>
public class EewControllerBasicTests : EewControllerTestBase
{
	private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	[Fact(DisplayName = "初期状態ではFoundがfalseであり、Clear後も同様")]
	public void 初期状態とClear()
	{
		Assert.False(Controller.Found);

		Controller.Update(CreateEew(), BaseTime);
		Assert.True(Controller.Found);

		Controller.Clear();
		Assert.False(Controller.Found);

		Assert.Single(EewUpdatedCalls);
	}

	[Fact(DisplayName = "新規EEW追加時にEewUpdatedイベントが発火し正しいEewが含まれる")]
	public void 新規EEW追加()
	{
		var eew = CreateEew(id: "event-001", serialNo: 1, maxIntensity: JmaIntensity.Int4);

		Controller.Update(eew, BaseTime);

		Assert.True(Controller.Found);
		Assert.Single(EewUpdatedCalls);
		var (updatedTime, eews) = EewUpdatedCalls[0];
		Assert.Equal(BaseTime, updatedTime);
		Assert.Single(eews);
		Assert.Equal("event-001", eews[0].Id);
		Assert.Equal(JmaIntensity.Int4, eews[0].MaxIntensity);
	}

	[Fact(DisplayName = "複数の異なるイベントIDのEEWが独立して管理される")]
	public void 複数EEW管理()
	{
		Controller.Update(CreateEew(id: "event-001"), BaseTime);
		Controller.Update(CreateEew(id: "event-002"), BaseTime);

		Assert.Equal(2, EewUpdatedCalls.Count);
		var lastEews = EewUpdatedCalls[^1].Eews;
		Assert.Equal(2, lastEews.Length);
		Assert.Contains(lastEews, e => e.Id == "event-001");
		Assert.Contains(lastEews, e => e.Id == "event-002");
	}

	[Fact(DisplayName = "IsReplayプロパティが設定・取得できる")]
	public void IsReplayプロパティ()
	{
		Assert.False(Controller.IsReplay);
		Controller.IsReplay = true;
		Assert.True(Controller.IsReplay);
	}

	// --- ShowDetails フィルタ ---

	[Fact(DisplayName = "ShowDetails=falseの場合に低精度EEWがスキップされる")]
	public void ShowDetailsオフで低精度EEWスキップ()
	{
		Config.Eew.ShowDetails = false;

		var lowAccuracyEew = CreateEew(
			id: "ev1",
			hypocenter: CreateDefaultHypocenter(locationAccuracy: 1, depthAccuracy: 1));

		Controller.Update(lowAccuracyEew, BaseTime);

		Assert.False(Controller.Found);
		Assert.Empty(EewUpdatedCalls);
	}

	[Fact(DisplayName = "ShowDetails=trueの場合は低精度EEWもスキップされない")]
	public void ShowDetailsオンで低精度EEWはスキップされない()
	{
		Config.Eew.ShowDetails = true;

		var lowAccuracyEew = CreateEew(
			id: "ev1",
			hypocenter: CreateDefaultHypocenter(locationAccuracy: 1, depthAccuracy: 1));

		Controller.Update(lowAccuracyEew, BaseTime);

		Assert.True(Controller.Found);
		Assert.Single(EewUpdatedCalls);
	}

	[Fact(DisplayName = "ShowDetails=falseでも既にキャッシュにあるEEWの更新はスキップされない")]
	public void ShowDetailsオフでも既存EEWは更新される()
	{
		Config.Eew.ShowDetails = true;
		Controller.Update(CreateEew(id: "ev1", serialNo: 1,
			hypocenter: CreateDefaultHypocenter(locationAccuracy: 1, depthAccuracy: 1)), BaseTime);
		Config.Eew.ShowDetails = false;
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 2,
			hypocenter: CreateDefaultHypocenter(locationAccuracy: 1, depthAccuracy: 1)), BaseTime);

		Assert.Single(EewUpdatedCalls);
	}

	[Fact(DisplayName = "ShowDetails=falseでも警報状態のEEWはスキップされない")]
	public void ShowDetailsオフでも警報EEWはスキップされない()
	{
		Config.Eew.ShowDetails = false;

		var warningEew = CreateEew(
			id: "ev1",
			isWarning: true,
			hypocenter: CreateDefaultHypocenter(locationAccuracy: 1, depthAccuracy: 1));

		Controller.Update(warningEew, BaseTime);

		Assert.True(Controller.Found);
		Assert.Single(EewUpdatedCalls);
	}

	// --- 状態遷移 ---

	[Fact(DisplayName = "最終報が更新として正しく処理される")]
	public void 最終報処理()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, isFinal: false), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 2, isFinal: true), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.True(eew.IsFinal);
	}

	[Fact(DisplayName = "最大震度の上昇と下降が正しく記録される")]
	public void 最大震度変動()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, maxIntensity: JmaIntensity.Int3), BaseTime);
		Controller.Update(CreateEew(id: "ev1", serialNo: 2, maxIntensity: JmaIntensity.Int5Lower), BaseTime);
		Controller.Update(CreateEew(id: "ev1", serialNo: 3, maxIntensity: JmaIntensity.Int4), BaseTime);

		Assert.Equal(3, EewUpdatedCalls.Count);
		Assert.Equal(JmaIntensity.Int5Lower, EewUpdatedCalls[1].Eews[0].MaxIntensity);
		Assert.Equal(JmaIntensity.Int4, EewUpdatedCalls[2].Eews[0].MaxIntensity);
	}

	[Fact(DisplayName = "更新でIsWarning=trueに変わった場合に正しく記録される")]
	public void 警報状態到達()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, isWarning: false), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 2, isWarning: true), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.True(eew.IsWarning);
	}
}
