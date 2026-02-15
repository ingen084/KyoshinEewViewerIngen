using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Services.Eew;

/// <summary>
/// EewControllerのMixEewロジック（ソース統合）のテスト
/// </summary>
public class EewControllerMixEewTests : EewControllerTestBase
{
	private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	[Fact(DisplayName = "同一イベントで報数が大きいEEWで更新される")]
	public void 報数が大きい場合更新()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, maxIntensity: JmaIntensity.Int3), BaseTime);
		Controller.Update(CreateEew(id: "ev1", serialNo: 2, maxIntensity: JmaIntensity.Int5Lower), BaseTime);

		Assert.Equal(2, EewUpdatedCalls.Count);
		var lastEews = EewUpdatedCalls[^1].Eews;
		Assert.Single(lastEews);
		Assert.Equal(2, lastEews[0].SerialNo);
		Assert.Equal(JmaIntensity.Int5Lower, lastEews[0].MaxIntensity);
	}

	[Fact(DisplayName = "同一イベントで報数が小さいEEWでは更新されない")]
	public void 報数が小さい場合更新されない()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 3), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 2), BaseTime);

		Assert.Empty(EewUpdatedCalls);
	}

	[Fact(DisplayName = "NewerSerialで更新時にIsCancelledとIsTrueCancelledが論理和で伝播する")]
	public void NewerSerial_キャンセルフラグ伝播()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1), BaseTime);
		Controller.Cancelled("ev1", BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 2, isCancelled: false), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.True(eew.IsCancelled);
		Assert.True(eew.IsTrueCancelled);
	}

	[Fact(DisplayName = "同一報数でKyoshinMonitorからDmdataに優先度更新される")]
	public void KyoshinMonitorからDmdata_MorePriority()
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: EewSource.KyoshinMonitor, maxIntensity: JmaIntensity.Int3), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: EewSource.Dmdata, maxIntensity: JmaIntensity.Int4), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.Equal(EewSource.Dmdata, eew.Source);
		Assert.Equal(JmaIntensity.Int4, eew.MaxIntensity);
	}

	[Fact(DisplayName = "同一報数でKyoshinMonitorからSNPに精度情報が移植される")]
	public void KyoshinMonitorからSNP_精度情報移植()
	{
		var kmAccuracy = new EewHypocenterAccuracy { IsLocked = false, LocationAccuracy = 3, DepthAccuracy = 3, MagnitudeAccuracy = 3 };
		var snpAccuracy = new EewHypocenterAccuracy { IsLocked = true, LocationAccuracy = 1, DepthAccuracy = 1, MagnitudeAccuracy = 1 };

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: EewSource.KyoshinMonitor,
			hypocenter: CreateDefaultHypocenter() with { Accuracy = kmAccuracy }), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: EewSource.SignalNowProfessional,
			hypocenter: CreateDefaultHypocenter() with { Accuracy = snpAccuracy }), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		// (KyoshinMonitor, SNP) ではcurrentがベースでreceivedの精度情報が移植される
		Assert.Equal(EewSource.KyoshinMonitor, eew.Source);
		Assert.Equal(snpAccuracy, eew.Hypocenter!.Accuracy);
	}

	[Theory(DisplayName = "同一報数でのソース優先度パターンが正しく動作する")]
	[InlineData(EewSource.SignalNowProfessional, EewSource.Dmdata, EewSource.Dmdata)]
	[InlineData(EewSource.Axis, EewSource.Dmdata, EewSource.Dmdata)]
	[InlineData(EewSource.KyoshinMonitor, EewSource.Dmdata, EewSource.Dmdata)]
	[InlineData(EewSource.SignalNowProfessional, EewSource.KyoshinMonitor, EewSource.KyoshinMonitor)]
	[InlineData(EewSource.KyoshinMonitor, EewSource.Axis, EewSource.Axis)]
	public void ソース優先度パターン(EewSource currentSource, EewSource receivedSource, EewSource expectedSource)
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: currentSource), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: receivedSource), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.Equal(expectedSource, eew.Source);
	}

	[Theory(DisplayName = "同一報数で更新されないソースパターン")]
	[InlineData(EewSource.Dmdata, EewSource.KyoshinMonitor)]
	[InlineData(EewSource.Dmdata, EewSource.Dmdata)]
	[InlineData(EewSource.Axis, EewSource.Axis)]
	[InlineData(EewSource.Dmdata, EewSource.SignalNowProfessional)]
	[InlineData(EewSource.Dmdata, EewSource.Axis)]
	[InlineData(EewSource.Axis, EewSource.KyoshinMonitor)]
	public void 更新されないソースパターン(EewSource currentSource, EewSource receivedSource)
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: currentSource), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: receivedSource), BaseTime);

		Assert.Empty(EewUpdatedCalls);
	}

	[Theory(DisplayName = "AccuracySupportでcurrent側がベースになるパターン")]
	[InlineData(EewSource.KyoshinMonitor, EewSource.SignalNowProfessional)]
	[InlineData(EewSource.Axis, EewSource.SignalNowProfessional)]
	public void AccuracySupport_currentベースパターン(EewSource currentSource, EewSource receivedSource)
	{
		var currentAccuracy = new EewHypocenterAccuracy { IsLocked = false, LocationAccuracy = 3, DepthAccuracy = 3, MagnitudeAccuracy = 3 };
		var receivedAccuracy = new EewHypocenterAccuracy { IsLocked = true, LocationAccuracy = 1, DepthAccuracy = 1, MagnitudeAccuracy = 1 };

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: currentSource,
			hypocenter: CreateDefaultHypocenter() with { Accuracy = currentAccuracy, Place = "元の震源地" }), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: receivedSource,
			hypocenter: CreateDefaultHypocenter() with { Accuracy = receivedAccuracy, Place = "新しい震源地" }), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		// currentがベースなのでSourceとPlaceはcurrent側
		Assert.Equal(currentSource, eew.Source);
		Assert.Equal("元の震源地", eew.Hypocenter!.Place);
		// Accuracyはreceived側から移植
		Assert.Equal(receivedAccuracy, eew.Hypocenter.Accuracy);
	}

	[Theory(DisplayName = "AccuracySupportでreceived側がベースになるパターン")]
	[InlineData(EewSource.SignalNowProfessional, EewSource.KyoshinMonitor)]
	[InlineData(EewSource.SignalNowProfessional, EewSource.Axis)]
	[InlineData(EewSource.KyoshinMonitor, EewSource.Axis)]
	public void AccuracySupport_receivedベースパターン(EewSource currentSource, EewSource receivedSource)
	{
		var currentAccuracy = new EewHypocenterAccuracy { IsLocked = true, LocationAccuracy = 2, DepthAccuracy = 2, MagnitudeAccuracy = 2 };
		var receivedAccuracy = new EewHypocenterAccuracy { IsLocked = false, LocationAccuracy = 4, DepthAccuracy = 4, MagnitudeAccuracy = 4 };

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: currentSource,
			hypocenter: CreateDefaultHypocenter() with { Accuracy = currentAccuracy, Place = "元の震源地" }), BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: receivedSource,
			hypocenter: CreateDefaultHypocenter() with { Accuracy = receivedAccuracy, Place = "新しい震源地" }), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		// receivedがベースなのでSourceはreceived側
		Assert.Equal(receivedSource, eew.Source);
		Assert.Equal("新しい震源地", eew.Hypocenter!.Place);
		// Accuracyはcurrent側から移植
		Assert.Equal(currentAccuracy, eew.Hypocenter.Accuracy);
	}

	// --- MixEew内のWarningAreas伝播 ---

	/// <param name="expectedFromCurrent">trueならcurrent側、falseならreceived側のWarningAreasが採用される</param>
	[Theory(DisplayName = "MixEewでWarningAreasが正しい側から採用される")]
	// AccuracySupport currentベース: received側のWarningAreasが移植される
	[InlineData(EewSource.KyoshinMonitor, EewSource.SignalNowProfessional, false)]
	[InlineData(EewSource.Axis, EewSource.SignalNowProfessional, false)]
	// AccuracySupport receivedベース: current側のWarningAreasが保持される
	[InlineData(EewSource.SignalNowProfessional, EewSource.KyoshinMonitor, true)]
	[InlineData(EewSource.SignalNowProfessional, EewSource.Axis, true)]
	[InlineData(EewSource.KyoshinMonitor, EewSource.Axis, true)]
	// MorePriority: received側のWarningAreasがそのまま採用される
	[InlineData(EewSource.KyoshinMonitor, EewSource.Dmdata, false)]
	[InlineData(EewSource.SignalNowProfessional, EewSource.Dmdata, false)]
	[InlineData(EewSource.Axis, EewSource.Dmdata, false)]
	public void MixEew_WarningAreas伝播(EewSource currentSource, EewSource receivedSource, bool expectedFromCurrent)
	{
		var currentAreas = new EewWarningAreas
		{
			DisplaySource = "current",
			SerialNo = 1,
			Codes = [100],
			Names = ["元の地域"],
			IsWarningTelegram = true,
		};
		var receivedAreas = new EewWarningAreas
		{
			DisplaySource = "received",
			SerialNo = 1,
			Codes = [100, 200],
			Names = ["新地域1", "新地域2"],
			IsWarningTelegram = true,
		};

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: currentSource,
			hypocenter: CreateDefaultHypocenter(locationAccuracy: 2, depthAccuracy: 2))
			with { WarningAreas = currentAreas }, BaseTime);
		EewUpdatedCalls.Clear();

		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: receivedSource,
			hypocenter: CreateDefaultHypocenter(locationAccuracy: 4, depthAccuracy: 4))
			with { WarningAreas = receivedAreas }, BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.Equal(expectedFromCurrent ? currentAreas : receivedAreas, eew.WarningAreas);
	}

	/// <summary>
	/// EewController.cs 322行目のバグ検出テスト
	/// (SNP/Axis/P2pQuakeApi, Dmdata) パターンで IsTrueCancelled の計算が
	/// received.IsCancelled || current.IsTrueCancelled になっており、
	/// 正しくは received.IsTrueCancelled || current.IsTrueCancelled であるべき
	/// </summary>
	[Theory(DisplayName = "Dmdata優先更新時にIsCancelledがIsTrueCancelledに誤って伝播しない")]
	[InlineData(EewSource.SignalNowProfessional)]
	[InlineData(EewSource.Axis)]
	public void Dmdata優先更新時のIsTrueCancelled計算(EewSource currentSource)
	{
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: currentSource), BaseTime);
		EewUpdatedCalls.Clear();

		// received: IsCancelled=true だが IsTrueCancelled=false（確実ではないキャンセル）
		Controller.Update(CreateEew(id: "ev1", serialNo: 1, source: EewSource.Dmdata,
			isCancelled: true, isTrueCancelled: false), BaseTime);

		Assert.Single(EewUpdatedCalls);
		var eew = Assert.Single(EewUpdatedCalls[0].Eews, e => e.Id == "ev1");
		Assert.True(eew.IsCancelled);
		// IsCancelled=true が IsTrueCancelled に伝播してはならない
		Assert.False(eew.IsTrueCancelled);
	}
}
