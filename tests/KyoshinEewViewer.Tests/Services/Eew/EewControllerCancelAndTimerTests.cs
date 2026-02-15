using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Services.Eew;

/// <summary>
/// EewControllerのキャンセル処理、警報管理、タイマー、マージロジックのテスト
/// </summary>
public class EewControllerCancelAndTimerTests : EewControllerTestBase
{
	private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	// --- キャンセル処理 ---

	[Fact(DisplayName = "eventId指定でEEWがキャンセルされる")]
	public void キャンセル_eventId指定()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Cancelled("ev1", BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.True(eew.IsCancelled);
		Assert.True(eew.IsTrueCancelled);
		Assert.True(Controller.Found);
	}

	[Fact(DisplayName = "既にキャンセル済みのEEWに対するキャンセルはスキップされる")]
	public void キャンセル_既にキャンセル済みスキップ()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1), BaseTime);
		Controller.Cancelled("ev1", BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Cancelled("ev1", BaseTime);

		Assert.Empty(EewUpdatedCalls);
	}

	[Fact(DisplayName = "eventId=nullで強震モニタソースのEEWのみ一括キャンセルされる")]
	public void キャンセル_一括キャンセル_強震モニタのみ()
	{
		var pastTime = BaseTime.AddMinutes(-1);

		Controller.Update(CreateEew(id: "ev1", source: EewSource.KyoshinMonitor, receiveTime: pastTime), pastTime);
		Controller.Update(CreateEew(id: "ev2", source: EewSource.Dmdata, receiveTime: pastTime), pastTime);
		EewUpdatedCalls.Clear();

		Controller.Cancelled(null, BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eews = EewUpdatedCalls[0].Eews;
		var kmEew = Assert.Single(eews, e => e.Id == "ev1");
		Assert.True(kmEew.IsCancelled);
		Assert.False(kmEew.IsTrueCancelled);
		var dmdataEew = Assert.Single(eews, e => e.Id == "ev2");
		Assert.False(dmdataEew.IsCancelled);
	}

	[Fact(DisplayName = "eventId=null一括キャンセルで最終報やキャンセル済みは対象外")]
	public void キャンセル_一括キャンセル_最終報とキャンセル済みは除外()
	{
		var pastTime = BaseTime.AddMinutes(-1);

		Controller.Update(CreateEew(id: "ev1", source: EewSource.KyoshinMonitor, isFinal: true, receiveTime: pastTime), pastTime);
		Controller.Update(CreateEew(id: "ev2", source: EewSource.KyoshinMonitor, receiveTime: pastTime), pastTime);
		Controller.Cancelled("ev2", BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Cancelled(null, BaseTime);

		Assert.Empty(EewUpdatedCalls);
	}

	[Fact(DisplayName = "存在しないeventIdでのキャンセルは何もしない")]
	public void キャンセル_存在しないeventId()
	{
		Controller.Cancelled("non-existent", BaseTime);
		Assert.Empty(EewUpdatedCalls);
	}

	// --- 警報管理 ---

	[Fact(DisplayName = "警報EEWの追加と報数更新が正しく動作する")]
	public void 警報EEW追加と更新()
	{
		var warningEew1 = CreateEew(id: "ev1", source: EewSource.Dmdata, serialNo: 1, isWarning: true);
		Controller.UpdateWarning(warningEew1, BaseTime);

		Assert.True(Controller.Found);
		Assert.Single(EewUpdatedCalls);

		var warningEew2 = CreateEew(id: "ev1", source: EewSource.Dmdata, serialNo: 2, isWarning: true, maxIntensity: JmaIntensity.Int5Lower);
		Controller.UpdateWarning(warningEew2, BaseTime);

		Assert.Equal(2, EewUpdatedCalls.Count);
		var lastEew = Assert.Single(EewUpdatedCalls[^1].Eews, e => e.Id == "ev1");
		Assert.Equal(2, lastEew.SerialNo);
	}

	[Fact(DisplayName = "警報EEWの報数が同じか小さい場合は更新されない")]
	public void 警報EEW_報数が小さい場合更新されない()
	{
		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 3, isWarning: true), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 3, isWarning: true), BaseTime);
		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 1, isWarning: true), BaseTime);

		Assert.Empty(EewUpdatedCalls);
	}

	[Fact(DisplayName = "同一報数の警報EEWでDmdataが他ソースより優先される")]
	public void 警報EEW_Dmdata優先()
	{
		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 1, source: EewSource.P2pQuakeApi, isWarning: true), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 1, source: EewSource.Dmdata, isWarning: true, maxIntensity: JmaIntensity.Int5Lower), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.Equal(EewSource.Dmdata, eew.Source);
		Assert.Equal(JmaIntensity.Int5Lower, eew.MaxIntensity);
	}

	[Fact(DisplayName = "Dmdataの警報EEWが同一報数の他ソースで上書きされない")]
	public void 警報EEW_Dmdata上書き不可()
	{
		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 1, source: EewSource.Dmdata, isWarning: true, maxIntensity: JmaIntensity.Int4), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 1, source: EewSource.P2pQuakeApi, isWarning: true), BaseTime);

		Assert.Empty(EewUpdatedCalls);
	}

	[Fact(DisplayName = "警報EEWのキャンセルが正しく処理される")]
	public void 警報EEWキャンセル()
	{
		Controller.UpdateWarning(CreateEew(id: "ev1", serialNo: 1, isWarning: true), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.WarningCancelled("ev1", BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.True(eew.IsCancelled);
		Assert.True(eew.IsTrueCancelled);
	}

	[Fact(DisplayName = "存在しない警報EEWのキャンセルは何もしない")]
	public void 警報EEWキャンセル_存在しない場合()
	{
		Controller.WarningCancelled("non-existent", BaseTime);
		Assert.Empty(EewUpdatedCalls);
	}

	// --- タイマー ---

	[Fact(DisplayName = "3分経過したEEWが期限切れとして削除される")]
	public void タイマー_3分経過で期限切れ削除()
	{
		Controller.Update(CreateEew(id: "ev1", receiveTime: BaseTime), BaseTime);
		Controller.Update(CreateEew(id: "ev2", receiveTime: BaseTime.AddMinutes(2)), BaseTime.AddMinutes(2));
		EewUpdatedCalls.Clear();

		Controller.TimerElapsed(BaseTime.AddMinutes(3));

		Assert.Single(EewUpdatedCalls);
		var eews = EewUpdatedCalls[0].Eews;
		Assert.Single(eews);
		Assert.Equal("ev2", eews[0].Id);
	}

	[Fact(DisplayName = "EewCacheの期限切れ時にWarningEewCacheも連動削除される")]
	public void タイマー_警報連動削除()
	{
		Controller.Update(CreateEew(id: "ev1", receiveTime: BaseTime), BaseTime);
		Controller.UpdateWarning(CreateEew(id: "ev1", receiveTime: BaseTime, isWarning: true), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.TimerElapsed(BaseTime.AddMinutes(3));

		Assert.Single(EewUpdatedCalls);
		Assert.False(Controller.Found);
	}

	[Fact(DisplayName = "3分未満のEEWに対してTimerElapsedはイベントを発火しない")]
	public void タイマー_期限切れなし()
	{
		Controller.Update(CreateEew(id: "ev1", receiveTime: BaseTime), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.TimerElapsed(BaseTime.AddMinutes(2));

		Assert.Empty(EewUpdatedCalls);
		Assert.True(Controller.Found);
	}

	[Fact(DisplayName = "警報のみのEEWも3分経過で期限切れ削除される")]
	public void タイマー_警報のみのEEW期限切れ()
	{
		Controller.UpdateWarning(CreateEew(id: "ev1", receiveTime: BaseTime, isWarning: true), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.TimerElapsed(BaseTime.AddMinutes(3));

		Assert.Single(EewUpdatedCalls);
		Assert.False(Controller.Found);
	}

	[Fact(DisplayName = "予報の期限切れ削除後もWarningEewCacheのみのEEWがEewUpdatedに含まれる")]
	public void タイマー_予報削除後に警報のみのEEWが含まれる()
	{
		Controller.Update(CreateEew(id: "ev1", receiveTime: BaseTime), BaseTime);
		Controller.UpdateWarning(CreateEew(id: "ev2", receiveTime: BaseTime.AddMinutes(2), isWarning: true), BaseTime.AddMinutes(2));
		EewUpdatedCalls.Clear();

		Controller.TimerElapsed(BaseTime.AddMinutes(3));

		Assert.Single(EewUpdatedCalls);
		var eews = EewUpdatedCalls[0].Eews;
		Assert.Contains(eews, e => e.Id == "ev2");
		Assert.True(Controller.Found);
	}

	// --- マージロジック ---

	/// <summary>
	/// 予報と警報のWarningAreas統合パターンをまとめたTheoryテスト
	/// </summary>
	/// <param name="forecastFirst">trueなら予報→警報の順、falseなら警報→予報の順</param>
	/// <param name="forecastWarningAreasType">
	/// 予報側のWarningAreasの状態:
	/// "telegram" = IsWarningTelegram=true,
	/// "non-telegram" = IsWarningTelegram=false,
	/// "null" = WarningAreasなし
	/// </param>
	[Theory(DisplayName = "予報と警報の受信順序やWarningAreasの状態に関わらずWarningEewCacheのWarningAreasで統合される")]
	// 予報が先で警報が後
	[InlineData(true, "telegram")]
	[InlineData(true, "non-telegram")]
	[InlineData(true, "null")]
	// 警報が先で予報が後
	[InlineData(false, "telegram")]
	[InlineData(false, "null")]
	public void EewUpdated_WarningAreas統合パターン(bool forecastFirst, string forecastWarningAreasType)
	{
		var warningEewAreas = new EewWarningAreas
		{
			DisplaySource = "dmdata",
			SerialNo = 1,
			Codes = [100, 200],
			Names = ["テスト地域", "テスト地域2"],
			IsWarningTelegram = true,
		};
		EewWarningAreas? forecastAreas = forecastWarningAreasType switch
		{
			"telegram" => new EewWarningAreas
			{
				DisplaySource = "test",
				SerialNo = 1,
				Codes = [100],
				Names = ["テスト地域"],
				IsWarningTelegram = true,
			},
			"non-telegram" => new EewWarningAreas
			{
				DisplaySource = "test",
				SerialNo = 1,
				Codes = [100],
				Names = ["テスト地域"],
				IsWarningTelegram = false,
			},
			_ => null,
		};

		var forecastEew = forecastAreas != null
			? CreateEew(id: "ev1") with { WarningAreas = forecastAreas }
			: CreateEew(id: "ev1");
		var warningEew = CreateEew(id: "ev1", isWarning: true) with { WarningAreas = warningEewAreas };

		if (forecastFirst)
		{
			Controller.Update(forecastEew, BaseTime);
			Controller.UpdateWarning(warningEew, BaseTime);
		}
		else
		{
			Controller.UpdateWarning(warningEew, BaseTime);
			Controller.Update(forecastEew, BaseTime);
		}

		var lastEews = EewUpdatedCalls[^1].Eews;
		Assert.Single(lastEews);
		Assert.Equal(warningEewAreas, lastEews[0].WarningAreas);
	}

	[Fact(DisplayName = "WarningEewCacheのみにあるEEWもEewUpdatedに含まれる")]
	public void EewUpdated_警報のみのEEWが含まれる()
	{
		Controller.Update(CreateEew(id: "ev1"), BaseTime);
		Controller.UpdateWarning(CreateEew(id: "ev2", isWarning: true), BaseTime);

		var lastEews = EewUpdatedCalls[^1].Eews;
		Assert.Equal(2, lastEews.Length);
		Assert.Contains(lastEews, e => e.Id == "ev1");
		Assert.Contains(lastEews, e => e.Id == "ev2");
	}
}
