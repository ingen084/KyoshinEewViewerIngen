using KyoshinEewViewer.Core;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Earthquake;

/// <summary>
/// EarthquakeEvent.SyncProperties の合成アルゴリズムをテストする
/// 複数のフラグメントが到着順に適用された際のプロパティ合成を検証する
/// </summary>
public class EarthquakeEventSyncPropertiesTests
{
	private static readonly DateTime BaseTime = new(2026, 1, 1, 12, 0, 0);
	private static readonly Location TestLocation = new(35.7f, 139.7f);

	private static EarthquakeInformationData CreateIntensityReport(
		DateTime? reportDateTime = null,
		JmaIntensity maxIntensity = JmaIntensity.Int4,
		string place = "東京都23区",
		DateTime? detectionTime = null)
	{
		return new EarthquakeInformationData
		{
			Title = "震度速報",
			EventId = "test-event",
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime ?? BaseTime,
			Source = "Test",
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
				DetectionTime = detectionTime ?? BaseTime.AddSeconds(-30),
				RepresentativeAreaName = place,
				IsOnlyArea = false,
			},
			ForecastComment = "震度速報コメント",
		};
	}

	private static EarthquakeInformationData CreateHypocenterReport(
		DateTime? reportDateTime = null,
		string place = "千葉県北西部",
		float magnitude = 4.5f,
		int depth = 70,
		DateTime? occurrenceTime = null,
		string? comment = null)
	{
		return new EarthquakeInformationData
		{
			Title = "震源に関する情報",
			EventId = "test-event",
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime ?? BaseTime.AddMinutes(3),
			Source = "Test",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = occurrenceTime ?? BaseTime.AddSeconds(-35),
				Place = place,
				Location = TestLocation,
				Magnitude = magnitude,
				Depth = depth,
			},
			ForecastComment = comment,
		};
	}

	private static EarthquakeInformationData CreateHypocenterAndIntensityReport(
		DateTime? reportDateTime = null,
		string place = "千葉県北西部",
		float magnitude = 4.5f,
		int depth = 70,
		JmaIntensity maxIntensity = JmaIntensity.Int4,
		bool isForeign = false,
		bool isVolcano = false,
		string? volcanoName = null,
		DateTime? occurrenceTime = null)
	{
		return new EarthquakeInformationData
		{
			Title = "震源・震度に関する情報",
			EventId = "test-event",
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime ?? BaseTime.AddMinutes(5),
			Source = "Test",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = occurrenceTime ?? BaseTime.AddSeconds(-35),
				Place = place,
				Location = TestLocation,
				Magnitude = magnitude,
				Depth = depth,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
			},
			IsForeign = isForeign,
			IsVolcano = isVolcano,
			VolcanoName = volcanoName,
			ForecastComment = "震源・震度コメント",
		};
	}

	private static EarthquakeInformationData CreateLpgmReport(
		DateTime? reportDateTime = null,
		JmaIntensity maxIntensity = JmaIntensity.Int4,
		LpgmIntensity lpgmIntensity = LpgmIntensity.LpgmInt2)
	{
		return new EarthquakeInformationData
		{
			Title = "長周期地震動に関する観測情報",
			EventId = "test-event",
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime ?? BaseTime.AddMinutes(8),
			Source = "Test",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = BaseTime.AddSeconds(-35),
				Place = "千葉県北西部",
				Location = TestLocation,
				Magnitude = 5.0f,
				Depth = 70,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
			},
			MaxLpgmIntensity = lpgmIntensity,
		};
	}

	private static EarthquakeInformationData CreateCancelReport(
		string title,
		DateTime? reportDateTime = null)
	{
		return new EarthquakeInformationData
		{
			Title = title,
			EventId = "test-event",
			InfoType = EarthquakeInfoType.Cancel,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime ?? BaseTime.AddMinutes(10),
			Source = "Test",
		};
	}

	private static EarthquakeInformationData CreateCorrectionReport(
		DateTime? reportDateTime = null,
		string place = "千葉県北西部",
		float magnitude = 4.8f,
		JmaIntensity maxIntensity = JmaIntensity.Int5Lower)
	{
		return new EarthquakeInformationData
		{
			Title = "震源・震度に関する情報",
			EventId = "test-event",
			InfoType = EarthquakeInfoType.Correction,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime ?? BaseTime.AddMinutes(15),
			Source = "Test",
			Hypocenter = new EarthquakeHypocenterData
			{
				OccurrenceTime = BaseTime.AddSeconds(-35),
				Place = place,
				Location = TestLocation,
				Magnitude = magnitude,
				Depth = 70,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
			},
		};
	}

	// ========== 単一フラグメントのテスト ==========

	[Fact(DisplayName = "震度速報のみ: IsSokuhouがtrueになり検知時刻が設定される")]
	public void IntensityReportOnly()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateIntensityReport());

		Assert.True(eq.IsSokuhou);
		Assert.False(eq.IsHypocenterOnly);
		Assert.False(eq.IsDetailIntensityApplied);
		Assert.Equal(JmaIntensity.Int4, eq.Intensity);
		Assert.Equal("東京都23区", eq.Place);
		Assert.True(eq.IsDetectionTime);
		Assert.Equal(-1, eq.Depth);
		Assert.Null(eq.Location);
	}

	[Fact(DisplayName = "震源情報のみ: IsHypocenterOnlyがtrueになり発生時刻が設定される")]
	public void HypocenterReportOnly()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterReport());

		Assert.False(eq.IsSokuhou);
		Assert.True(eq.IsHypocenterOnly);
		Assert.False(eq.IsDetailIntensityApplied);
		Assert.Equal("千葉県北西部", eq.Place);
		Assert.False(eq.IsDetectionTime);
		Assert.Equal(4.5f, eq.Magnitude);
		Assert.Equal(70, eq.Depth);
		Assert.Equal(TestLocation, eq.Location);
	}

	[Fact(DisplayName = "震源・震度情報のみ: IsDetailIntensityAppliedがtrueになる")]
	public void HypocenterAndIntensityReportOnly()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport());

		Assert.False(eq.IsSokuhou);
		Assert.False(eq.IsHypocenterOnly);
		Assert.True(eq.IsDetailIntensityApplied);
		Assert.Equal(JmaIntensity.Int4, eq.Intensity);
		Assert.Equal("千葉県北西部", eq.Place);
		Assert.Equal(4.5f, eq.Magnitude);
		Assert.Equal(70, eq.Depth);
		Assert.Equal(TestLocation, eq.Location);
	}

	// ========== 複数フラグメントの合成シナリオ ==========

	[Fact(DisplayName = "震度速報→震源・震度情報: IsSokuhouがfalseに切り替わり震源情報で上書きされる")]
	public void IntensityThenHypocenterAndIntensity()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateIntensityReport(place: "東京都23区"));

		Assert.True(eq.IsSokuhou);
		Assert.Equal("東京都23区", eq.Place);

		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport(
			place: "千葉県北西部", maxIntensity: JmaIntensity.Int4));

		Assert.False(eq.IsSokuhou);
		Assert.False(eq.IsHypocenterOnly);
		Assert.True(eq.IsDetailIntensityApplied);
		Assert.Equal("千葉県北西部", eq.Place);
		Assert.Equal(JmaIntensity.Int4, eq.Intensity);
		Assert.False(eq.IsDetectionTime);
		Assert.Equal(TestLocation, eq.Location);
	}

	[Fact(DisplayName = "震度速報→震源情報: IsSokuhouとIsHypocenterOnlyの両方がtrueになる")]
	public void IntensityThenHypocenter()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateIntensityReport());
		eq.ProcessIntermediateData(CreateHypocenterReport());

		// 震度速報のフラグは残る（IsDetailIntensityAppliedがfalseのため）
		Assert.True(eq.IsSokuhou);
		// 震源情報が来たのでIsHypocenterOnlyもtrue
		Assert.True(eq.IsHypocenterOnly);
		Assert.False(eq.IsDetailIntensityApplied);

		// 震源情報の内容で上書き
		Assert.Equal("千葉県北西部", eq.Place);
		Assert.Equal(TestLocation, eq.Location);
		Assert.Equal(4.5f, eq.Magnitude);
		Assert.False(eq.IsDetectionTime);

		// Titleの算出: IsSokuhou=true && IsHypocenterOnly=true → "震度速報+震源情報"
		Assert.Equal("震度速報+震源情報", eq.Title);
	}

	[Fact(DisplayName = "震源情報→震源・震度情報: IsHypocenterOnlyがfalseに切り替わる")]
	public void HypocenterThenHypocenterAndIntensity()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterReport(place: "千葉県北西部", magnitude: 4.0f));

		Assert.True(eq.IsHypocenterOnly);
		Assert.Equal(4.0f, eq.Magnitude);

		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport(
			place: "千葉県北西部", magnitude: 4.5f, maxIntensity: JmaIntensity.Int4));

		Assert.False(eq.IsHypocenterOnly);
		Assert.True(eq.IsDetailIntensityApplied);
		Assert.Equal(4.5f, eq.Magnitude);
		Assert.Equal(JmaIntensity.Int4, eq.Intensity);
	}

	[Fact(DisplayName = "震度速報→震源情報→震源・震度情報: 最終的にIsDetailIntensityAppliedのみtrue")]
	public void FullSequence_IntensityHypocenterDetail()
	{
		var eq = new EarthquakeEvent("test-event");

		// 震度速報
		eq.ProcessIntermediateData(CreateIntensityReport(
			maxIntensity: JmaIntensity.Int3, place: "東京都23区"));
		Assert.True(eq.IsSokuhou);

		// 震源情報
		eq.ProcessIntermediateData(CreateHypocenterReport(place: "千葉県北西部"));
		Assert.True(eq.IsSokuhou);
		Assert.True(eq.IsHypocenterOnly);

		// 震源・震度情報
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport(
			place: "千葉県北西部", maxIntensity: JmaIntensity.Int4));

		Assert.False(eq.IsSokuhou);
		Assert.False(eq.IsHypocenterOnly);
		Assert.True(eq.IsDetailIntensityApplied);
		Assert.Equal(JmaIntensity.Int4, eq.Intensity);
		Assert.Equal("千葉県北西部", eq.Place);
	}

	// ========== ガード条件のテスト ==========

	[Fact(DisplayName = "震源・震度情報の後に震度速報が来てもPlaceが上書きされない")]
	public void DetailThenIntensity_PlaceNotOverwritten()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport(place: "千葉県北西部"));

		Assert.True(eq.IsDetailIntensityApplied);
		Assert.Equal("千葉県北西部", eq.Place);

		// 後から震度速報が来る（異なるReportDateTime）
		eq.ProcessIntermediateData(CreateIntensityReport(
			reportDateTime: BaseTime.AddMinutes(10), place: "東京都多摩東部"));

		// IsDetailIntensityApplied=true なので Place は上書きされない
		Assert.Equal("千葉県北西部", eq.Place);
		// ただしIntensityは上書きされる（ガードなし）
	}

	[Fact(DisplayName = "震源情報の後に震度速報が来てもPlaceが上書きされない")]
	public void HypocenterThenIntensity_PlaceNotOverwritten()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterReport(place: "千葉県北西部"));

		Assert.True(eq.IsHypocenterOnly);
		Assert.Equal("千葉県北西部", eq.Place);

		eq.ProcessIntermediateData(CreateIntensityReport(
			reportDateTime: BaseTime.AddMinutes(10), place: "東京都23区"));

		// IsHypocenterOnly=true なので Place は上書きされない
		Assert.Equal("千葉県北西部", eq.Place);
	}

	// ========== 長周期地震動 ==========

	[Fact(DisplayName = "長周期地震動: 継承チェーンにより震源情報と震度情報も同時に適用される")]
	public void LpgmReport_AppliesAllInheritedBlocks()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateLpgmReport(
			maxIntensity: JmaIntensity.Int4, lpgmIntensity: LpgmIntensity.LpgmInt3));

		// HypocenterInformationFragment ブロック
		Assert.Equal("千葉県北西部", eq.Place);
		Assert.Equal(TestLocation, eq.Location);
		Assert.Equal(5.0f, eq.Magnitude);
		Assert.Equal(70, eq.Depth);

		// HypocenterAndIntensityInformationFragment ブロック
		Assert.True(eq.IsDetailIntensityApplied);
		Assert.Equal(JmaIntensity.Int4, eq.Intensity);

		// LpgmIntensityInformationFragment ブロック
		Assert.Equal(LpgmIntensity.LpgmInt3, eq.LpgmIntensity);
	}

	[Fact(DisplayName = "震源・震度情報の後に長周期地震動が来るとLpgmIntensityが設定される")]
	public void DetailThenLpgm()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport());

		Assert.Null(eq.LpgmIntensity);

		eq.ProcessIntermediateData(CreateLpgmReport(lpgmIntensity: LpgmIntensity.LpgmInt2));

		Assert.Equal(LpgmIntensity.LpgmInt2, eq.LpgmIntensity);
		Assert.True(eq.IsDetailIntensityApplied);
	}

	// ========== 特殊フラグ ==========

	[Fact(DisplayName = "遠地地震情報: IsForeignがtrueに設定される")]
	public void ForeignEarthquake()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport(isForeign: true));

		Assert.True(eq.IsForeign);
		Assert.Equal("遠地地震情報", eq.Title);
	}

	[Fact(DisplayName = "大規模噴火: IsVolcanoがtrueに設定される")]
	public void VolcanoEarthquake()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport(
			isVolcano: true, volcanoName: "諏訪之瀬島"));

		Assert.True(eq.IsVolcano);
		Assert.Equal("諏訪之瀬島", eq.VolcanoName);
		Assert.Equal("大規模噴火", eq.Title);
	}

	// ========== 取消・訂正 ==========

	[Fact(DisplayName = "取消: 対象タイトルのフラグメントがIsCancelledになる")]
	public void CancelReport()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateIntensityReport());
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport());

		Assert.False(eq.IsCancelled);
		Assert.Equal(2, eq.Fragments.Count);

		// 震度速報を取消
		eq.ProcessIntermediateData(CreateCancelReport("震度速報"));

		// 震度速報フラグメントだけが取消になる
		Assert.True(eq.Fragments[0].IsCancelled);
		Assert.False(eq.Fragments[1].IsCancelled);
		// 全体としては取消ではない（震源・震度情報が生きている）
		Assert.False(eq.IsCancelled);
	}

	[Fact(DisplayName = "全フラグメント取消: IsCancelledがtrueになる")]
	public void CancelAllFragments()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateIntensityReport());

		eq.ProcessIntermediateData(CreateCancelReport("震度速報"));

		Assert.True(eq.IsCancelled);
	}

	[Fact(DisplayName = "訂正: 前のフラグメントがIsCorrectedになり新しいフラグメントが追加される")]
	public void CorrectionReport()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateHypocenterAndIntensityReport(
			magnitude: 4.5f, maxIntensity: JmaIntensity.Int4));

		Assert.Equal(4.5f, eq.Magnitude);
		Assert.Equal(JmaIntensity.Int4, eq.Intensity);

		// 訂正
		eq.ProcessIntermediateData(CreateCorrectionReport(
			magnitude: 4.8f, maxIntensity: JmaIntensity.Int5Lower));

		// 前のフラグメントが訂正済み
		Assert.True(eq.Fragments[0].IsCorrected);
		// 新しいフラグメントが追加
		Assert.Equal(2, eq.Fragments.Count);
		// 訂正後の値が反映（訂正済みフラグメントはSyncPropertiesでスキップされる）
		Assert.Equal(4.8f, eq.Magnitude);
		Assert.Equal(JmaIntensity.Int5Lower, eq.Intensity);
	}

	// ========== 訓練・試験 ==========

	[Fact(DisplayName = "訓練フラグメントが含まれるとIsTrainingがtrueになる")]
	public void TrainingFlag()
	{
		var eq = new EarthquakeEvent("test-event");
		var data = CreateIntensityReport();
		data = new EarthquakeInformationData
		{
			Title = data.Title,
			EventId = data.EventId,
			InfoType = data.InfoType,
			Status = EarthquakeReportStatus.Training,
			ReportDateTime = data.ReportDateTime,
			Source = data.Source,
			Intensity = data.Intensity,
		};
		eq.ProcessIntermediateData(data);

		Assert.True(eq.IsTraining);
	}

	// ========== Commentの特殊扱い ==========

	[Fact(DisplayName = "震源情報のCommentがnullの場合、前のCommentが保持される")]
	public void HypocenterNullComment_PreservesExisting()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateIntensityReport());

		Assert.Equal("震度速報コメント", eq.Comment);

		// 震源情報（Commentなし）
		eq.ProcessIntermediateData(CreateHypocenterReport(comment: null));

		// 震度速報のCommentが保持される
		Assert.Equal("震度速報コメント", eq.Comment);
	}

	[Fact(DisplayName = "震源情報のCommentがある場合は上書きされる")]
	public void HypocenterWithComment_Overrides()
	{
		var eq = new EarthquakeEvent("test-event");
		eq.ProcessIntermediateData(CreateIntensityReport());

		eq.ProcessIntermediateData(CreateHypocenterReport(comment: "震源情報コメント"));

		Assert.Equal("震源情報コメント", eq.Comment);
	}
}
