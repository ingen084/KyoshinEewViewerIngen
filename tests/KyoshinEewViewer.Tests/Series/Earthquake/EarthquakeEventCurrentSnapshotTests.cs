using KyoshinEewViewer.Core;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using Xunit;

namespace KyoshinEewViewer.Tests.Series.Earthquake;

public class EarthquakeEventCurrentSnapshotTests
{
	private static IntensityInformationFragment CreateSokuhou(DateTime detectionTime, JmaIntensity max = JmaIntensity.Int4, string title = "震度速報", string place = "宮城県北部", string? comment = null, bool isSingleArea = false)
		=> new()
		{
			ArrivedTime = detectionTime,
			Title = title,
			IsTraining = false,
			IsTest = false,
			Place = place,
			DetectionTime = detectionTime,
			MaxIntensity = max,
			IsSingleArea = isSingleArea,
			Comment = comment,
			Observation = [],
		};

	private static HypocenterInformationFragment CreateHypocenter(DateTime occurrence, string title = "震源に関する情報", string place = "宮城県沖", float magnitude = 6.8f, int depth = 50, string? comment = null)
		=> new()
		{
			ArrivedTime = occurrence,
			Title = title,
			IsTraining = false,
			IsTest = false,
			OccurrenceTime = occurrence,
			Place = place,
			Location = new Location(38.5f, 142.1f),
			LocationError = null,
			Magnitude = magnitude,
			MagnitudeAlternativeText = null,
			Depth = depth,
			DepthError = null,
			Comment = comment,
		};

	private static HypocenterAndIntensityInformationFragment CreateHypoAndIntensity(DateTime occurrence, JmaIntensity max = JmaIntensity.Int4, string place = "宮城県沖", float magnitude = 6.8f, string? comment = null, bool isForeign = false, bool isVolcano = false)
		=> new()
		{
			ArrivedTime = occurrence,
			Title = "震源・震度に関する情報",
			IsTraining = false,
			IsTest = false,
			OccurrenceTime = occurrence,
			Place = place,
			Location = new Location(38.5f, 142.1f),
			LocationError = null,
			Magnitude = magnitude,
			MagnitudeAlternativeText = null,
			Depth = 50,
			DepthError = null,
			MaxIntensity = max,
			IsForeign = isForeign,
			IsVolcano = isVolcano,
			Comment = comment,
			Observation = [],
		};

	private static LpgmIntensityInformationFragment CreateLpgm(DateTime occurrence, LpgmIntensity max = LpgmIntensity.LpgmInt2, string? comment = null)
		=> new()
		{
			ArrivedTime = occurrence,
			Title = "長周期地震動に関する観測情報",
			IsTraining = false,
			IsTest = false,
			OccurrenceTime = occurrence,
			Place = "宮城県沖",
			Location = new Location(38.5f, 142.1f),
			LocationError = null,
			Magnitude = 6.8f,
			MagnitudeAlternativeText = null,
			Depth = 50,
			DepthError = null,
			MaxIntensity = JmaIntensity.Int4,
			MaxLpgmIntensity = max,
			IsForeign = false,
			IsVolcano = false,
			Comment = comment,
			Observation = [],
		};

	[Fact(DisplayName = "震度速報単独受信時、OriginTimeはnull・ArrivalTimeはDetectionTimeになる")]
	public void Sokuhou_Only_Sets_ArrivalTime_Only()
	{
		var detection = new DateTime(2026, 4, 30, 14, 30, 0);
		var ev = new EarthquakeEvent("EVT-001");
		ev.AddFragment(CreateSokuhou(detection));

		Assert.NotNull(ev.CurrentSnapshot);
		Assert.Null(ev.CurrentSnapshot.Hypocenter);
		Assert.Equal(detection, ev.CurrentSnapshot.DetectionTime);

		Assert.Null(ev.OriginTime);
		Assert.Equal(detection, ev.ArrivalTime);
		Assert.Equal(detection, ev.Time);
		Assert.True(ev.IsDetectionTime);
	}

	[Fact(DisplayName = "震度速報→震源・震度情報で OriginTimeとArrivalTimeが両方保持される")]
	public void Sokuhou_Then_Detail_Keeps_Both_Times()
	{
		var detection = new DateTime(2026, 4, 30, 14, 30, 0);
		var occurrence = new DateTime(2026, 4, 30, 14, 29, 50);
		var ev = new EarthquakeEvent("EVT-002");
		ev.AddFragment(CreateSokuhou(detection));
		ev.AddFragment(CreateHypoAndIntensity(occurrence));

		Assert.NotNull(ev.CurrentSnapshot);
		Assert.NotNull(ev.CurrentSnapshot.Hypocenter);
		Assert.Equal(occurrence, ev.CurrentSnapshot.Hypocenter.OriginTime);
		Assert.Equal(detection, ev.CurrentSnapshot.DetectionTime);

		Assert.Equal(occurrence, ev.OriginTime);
		Assert.Equal(detection, ev.ArrivalTime);
		Assert.Equal(occurrence, ev.Time);
		Assert.False(ev.IsDetectionTime);
	}

	[Fact(DisplayName = "Tsunami経由の震源情報単独でArrivalTimeはnull・OriginTimeのみ")]
	public void TsunamiHypocenter_Only_Sets_OriginTime_Only()
	{
		var occurrence = new DateTime(2026, 4, 30, 14, 30, 0);
		var ev = new EarthquakeEvent("EVT-003");
		ev.AddFragment(CreateHypocenter(occurrence));

		Assert.NotNull(ev.CurrentSnapshot);
		Assert.NotNull(ev.CurrentSnapshot.Hypocenter);
		Assert.Equal(occurrence, ev.CurrentSnapshot.Hypocenter.OriginTime);
		Assert.Null(ev.CurrentSnapshot.DetectionTime);

		Assert.Equal(occurrence, ev.OriginTime);
		Assert.Null(ev.ArrivalTime);
	}

	[Fact(DisplayName = "続報を取消すと CurrentSnapshot がそれ以前の状態に巻き戻る")]
	public void CancellingLatestFragment_RollsBack_CurrentSnapshot()
	{
		var occurrence1 = new DateTime(2026, 4, 30, 14, 29, 50);
		var occurrence2 = new DateTime(2026, 4, 30, 14, 30, 30);
		var ev = new EarthquakeEvent("EVT-004");

		var first = CreateHypoAndIntensity(occurrence1, max: JmaIntensity.Int3, place: "場所A", magnitude: 5.0f);
		var second = CreateHypoAndIntensity(occurrence2, max: JmaIntensity.Int4, place: "場所B", magnitude: 6.5f);
		ev.AddFragment(first);
		ev.AddFragment(second);

		Assert.Equal("場所B", ev.Place);
		Assert.Equal(6.5f, ev.Magnitude);

		second.IsCancelled = true;
		ev.Resync();

		Assert.NotNull(ev.CurrentSnapshot);
		Assert.NotNull(ev.CurrentSnapshot.Hypocenter);
		Assert.Equal("場所A", ev.CurrentSnapshot.Hypocenter.Place);
		Assert.Equal(5.0f, ev.CurrentSnapshot.Hypocenter.Magnitude);
		Assert.Equal("場所A", ev.Place);
		Assert.Equal(5.0f, ev.Magnitude);
	}

	[Fact(DisplayName = "全Fragment取消で CurrentSnapshot は null かつ IsCancelled=true")]
	public void AllFragmentsCancelled_ClearsSnapshot_And_SetsCancelled()
	{
		var occurrence = new DateTime(2026, 4, 30, 14, 30, 0);
		var ev = new EarthquakeEvent("EVT-005");

		var hypo = CreateHypocenter(occurrence);
		ev.AddFragment(hypo);

		hypo.IsCancelled = true;
		ev.Resync();

		Assert.Null(ev.CurrentSnapshot);
		Assert.True(ev.IsCancelled);
	}

	[Fact(DisplayName = "顕著な地震の震源要素更新のお知らせ受信後、Tsunami由来の震源情報で震源要素が上書きされない")]
	public void NoticeableUpdate_LocksHypocenter_Against_Tsunami()
	{
		var ev = new EarthquakeEvent("EVT-006");

		ev.AddFragment(CreateHypoAndIntensity(
			new DateTime(2026, 4, 30, 14, 29, 50),
			place: "初期震央",
			magnitude: 5.5f));

		var noticeable = CreateHypocenter(
			new DateTime(2026, 4, 30, 14, 35, 0),
			title: "顕著な地震の震源要素更新のお知らせ",
			place: "更新後震央",
			magnitude: 6.8f,
			depth: 30);
		ev.AddFragment(noticeable);

		Assert.Equal("更新後震央", ev.Place);
		Assert.Equal(6.8f, ev.Magnitude);

		ev.AddFragment(CreateHypocenter(
			new DateTime(2026, 4, 30, 14, 40, 0),
			title: "津波警報・注意報・予報a",
			place: "Tsunami震央",
			magnitude: 7.2f,
			depth: 100));

		Assert.NotNull(ev.CurrentSnapshot);
		Assert.NotNull(ev.CurrentSnapshot.Hypocenter);
		Assert.Equal("更新後震央", ev.CurrentSnapshot.Hypocenter.Place);
		Assert.Equal(6.8f, ev.CurrentSnapshot.Hypocenter.Magnitude);
		Assert.Equal("更新後震央", ev.Place);
		Assert.Equal(6.8f, ev.Magnitude);
	}

	[Fact(DisplayName = "顕著な地震の震源要素更新のお知らせを取消すとロックが解除される")]
	public void CancellingNoticeableUpdate_UnlocksHypocenter()
	{
		var ev = new EarthquakeEvent("EVT-007");

		ev.AddFragment(CreateHypoAndIntensity(
			new DateTime(2026, 4, 30, 14, 29, 50),
			place: "初期震央",
			magnitude: 5.5f));

		var noticeable = CreateHypocenter(
			new DateTime(2026, 4, 30, 14, 35, 0),
			title: "顕著な地震の震源要素更新のお知らせ",
			place: "顕著震央",
			magnitude: 6.8f);
		ev.AddFragment(noticeable);

		noticeable.IsCancelled = true;
		ev.Resync();

		// 続報 HypocenterAndIntensity が来ればロック解除されているので採用される
		ev.AddFragment(CreateHypoAndIntensity(
			new DateTime(2026, 4, 30, 14, 40, 0),
			place: "続報震央",
			magnitude: 7.0f));

		Assert.NotNull(ev.CurrentSnapshot);
		Assert.NotNull(ev.CurrentSnapshot.Hypocenter);
		Assert.Equal("続報震央", ev.CurrentSnapshot.Hypocenter.Place);
		Assert.Equal(7.0f, ev.CurrentSnapshot.Hypocenter.Magnitude);
	}

	[Fact(DisplayName = "震度速報+単独震源情報でTitleとバッジが正しく出る")]
	public void Sokuhou_Plus_HypocenterOnly_ShowsBothBadges()
	{
		var detection = new DateTime(2026, 4, 30, 14, 30, 0);
		var occurrence = new DateTime(2026, 4, 30, 14, 29, 50);
		var ev = new EarthquakeEvent("EVT-009");

		ev.AddFragment(CreateSokuhou(detection, comment: "速報コメント"));
		ev.AddFragment(CreateHypocenter(occurrence, comment: "震源コメント"));

		Assert.True(ev.IsSokuhou);
		Assert.True(ev.IsHypocenterOnly);
		Assert.False(ev.IsDetailIntensityApplied);
		Assert.Equal("震度速報+震源情報", ev.Title);

		Assert.Equal("震源コメント", ev.Comment);

		Assert.NotNull(ev.CurrentSnapshot);
		Assert.NotNull(ev.CurrentSnapshot.Hypocenter);
		Assert.Equal(occurrence, ev.CurrentSnapshot.Hypocenter.OriginTime);
		Assert.Equal(detection, ev.CurrentSnapshot.DetectionTime);
	}

	[Fact(DisplayName = "顕著な地震の更新のCommentは採用されない")]
	public void NoticeableUpdate_Comment_Is_Ignored()
	{
		var ev = new EarthquakeEvent("EVT-010");

		ev.AddFragment(CreateSokuhou(
			new DateTime(2026, 4, 30, 14, 29, 50),
			comment: "速報コメント"));
		ev.AddFragment(CreateHypocenter(
			new DateTime(2026, 4, 30, 14, 35, 0),
			title: "顕著な地震の震源要素更新のお知らせ",
			comment: "顕著の更新コメント"));

		Assert.Equal("速報コメント", ev.Comment);
	}

	[Fact(DisplayName = "LPGM Fragment + Detail FragmentでCommentはLPGM由来")]
	public void Lpgm_Plus_Detail_CommentSourceIsLpgm()
	{
		var ev = new EarthquakeEvent("EVT-011");

		ev.AddFragment(CreateHypoAndIntensity(
			new DateTime(2026, 4, 30, 14, 29, 50),
			comment: "Detailコメント"));
		ev.AddFragment(CreateLpgm(
			new DateTime(2026, 4, 30, 14, 30, 30),
			comment: "LPGMコメント"));

		Assert.Equal("LPGMコメント", ev.Comment);
		Assert.Equal(EarthquakeIntensityScope.Detail, ev.Scope);
	}

	[Fact(DisplayName = "震度速報受信後に震源震度情報を受信すると IsSingleArea がリセットされる (Hypocenter≠nullなら true)")]
	public void DetailReceivedAfterSokuhou_ResetsIsSingleArea()
	{
		var detection = new DateTime(2026, 4, 30, 14, 30, 0);
		var occurrence = new DateTime(2026, 4, 30, 14, 30, 30);
		var ev = new EarthquakeEvent("EVT-012");

		// 震度速報のみ受信時は IsSingleArea が震度速報の値を反映
		ev.AddFragment(CreateSokuhou(detection, isSingleArea: true));
		Assert.True(ev.IsSingleArea);

		// 震源震度情報受信後は Hypocenter が立つので IsSingleArea = true (= 「など」非表示)
		ev.AddFragment(CreateHypoAndIntensity(occurrence));
		Assert.True(ev.IsSingleArea);
		Assert.NotNull(ev.CurrentSnapshot?.Hypocenter);
	}

	[Fact(DisplayName = "震度速報単独受信で複数地域なら IsSingleArea=false (「など」表示)")]
	public void Sokuhou_MultipleAreas_IsSingleAreaFalse()
	{
		var detection = new DateTime(2026, 4, 30, 14, 30, 0);
		var ev = new EarthquakeEvent("EVT-013");
		ev.AddFragment(CreateSokuhou(detection, isSingleArea: false));

		Assert.False(ev.IsSingleArea);
	}

	[Fact(DisplayName = "Time / IsDetectionTime getter の派生動作")]
	public void Time_And_IsDetectionTime_Derived_Behaviour()
	{
		var detection = new DateTime(2026, 4, 30, 14, 31, 0);
		var occurrence = new DateTime(2026, 4, 30, 14, 32, 0);

		var ev = new EarthquakeEvent("EVT-008");

		ev.AddFragment(CreateHypocenter(occurrence));
		Assert.Equal(occurrence, ev.Time);
		Assert.False(ev.IsDetectionTime);

		var ev2 = new EarthquakeEvent("EVT-008-2");
		ev2.AddFragment(CreateSokuhou(detection));
		Assert.Equal(detection, ev2.Time);
		Assert.True(ev2.IsDetectionTime);

		var ev3 = new EarthquakeEvent("EVT-008-3");
		ev3.AddFragment(CreateSokuhou(detection));
		ev3.AddFragment(CreateHypoAndIntensity(occurrence));
		Assert.Equal(occurrence, ev3.Time);
		Assert.False(ev3.IsDetectionTime);
	}
}
