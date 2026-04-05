using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Earthquake;

public class EarthquakeEventObservationTrackingTests
{
	private static EarthquakeInformationData CreateIntensityData(
		string title = "震度速報",
		string eventId = "test-event",
		DateTime? reportDateTime = null,
		JmaIntensity maxIntensity = JmaIntensity.Int3,
		EarthquakeObservationPref[]? observationPrefs = null)
	{
		return new EarthquakeInformationData
		{
			Title = title,
			EventId = eventId,
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = reportDateTime ?? new DateTime(2026, 1, 1, 12, 0, 0),
			Source = "Test",
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = maxIntensity,
				DetectionTime = new DateTime(2026, 1, 1, 11, 59, 0),
				RepresentativeAreaName = "テスト地域",
				IsOnlyArea = true,
				ObservationPrefs = observationPrefs,
			},
		};
	}

	private static EarthquakeObservationPref[] CreateTestPrefs(params (int Code, string Name, JmaIntensity Intensity)[] areas)
	{
		return [new EarthquakeObservationPref
		{
			Name = "テスト県",
			Code = 1,
			Areas = areas.Select(a => new EarthquakeObservationArea
			{
				Code = a.Code,
				Name = a.Name,
				MaxIntensity = a.Intensity,
			}).ToArray(),
		}];
	}

	[Fact(DisplayName = "初回の情報受信でLatestObservationPrefsが設定される")]
	public void FirstReception_SetsLatestObservationPrefs()
	{
		var eq = new EarthquakeEvent("test-event");
		var prefs = CreateTestPrefs((350, "東京都23区", JmaIntensity.Int3));
		var data = CreateIntensityData(observationPrefs: prefs);

		var result = eq.ProcessIntermediateData(data);

		Assert.NotNull(result.Fragment);
		Assert.Null(result.PreviousState);
		Assert.NotNull(eq.LatestObservationPrefs);
		Assert.Single(eq.LatestObservationPrefs![0].Areas);
		Assert.Equal(350, eq.LatestObservationPrefs[0].Areas[0].Code);
	}

	[Fact(DisplayName = "続報でLatestObservationPrefsが更新され前回状態が返される")]
	public void SubsequentReception_UpdatesAndReturnsPreviousState()
	{
		var eq = new EarthquakeEvent("test-event");

		// 初回
		var prefs1 = CreateTestPrefs((350, "東京都23区", JmaIntensity.Int3));
		var data1 = CreateIntensityData(observationPrefs: prefs1);
		var result1 = eq.ProcessIntermediateData(data1);
		Assert.Null(result1.PreviousState);

		// 続報（異なるReportDateTime）
		var prefs2 = CreateTestPrefs(
			(350, "東京都23区", JmaIntensity.Int3),
			(351, "東京都多摩東部", JmaIntensity.Int2)
		);
		var data2 = CreateIntensityData(
			observationPrefs: prefs2,
			reportDateTime: new DateTime(2026, 1, 1, 12, 5, 0)
		);
		var result2 = eq.ProcessIntermediateData(data2);

		Assert.NotNull(result2.Fragment);
		Assert.NotNull(result2.PreviousState);
		Assert.Equal(JmaIntensity.Int3, result2.PreviousState!.Intensity);
		Assert.Same(prefs1, result2.PreviousState.ObservationPrefs);

		Assert.NotNull(eq.LatestObservationPrefs);
		Assert.Equal(2, eq.LatestObservationPrefs![0].Areas.Length);
	}

	[Fact(DisplayName = "重複電文ではフラグメントがnullとなり観測データは変わらない")]
	public void DuplicateTelegram_ReturnsNull()
	{
		var eq = new EarthquakeEvent("test-event");
		var prefs = CreateTestPrefs((350, "東京都23区", JmaIntensity.Int3));
		var data = CreateIntensityData(observationPrefs: prefs);

		// 初回
		var result1 = eq.ProcessIntermediateData(data);
		Assert.NotNull(result1.Fragment);

		// 同一ReportDateTimeの重複（異なるソース）
		var duplicateData = new EarthquakeInformationData
		{
			Title = data.Title,
			EventId = data.EventId,
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = data.ReportDateTime,
			Source = "DifferentSource",
			Intensity = data.Intensity,
		};
		var result2 = eq.ProcessIntermediateData(duplicateData);

		Assert.Null(result2.Fragment);
		// 観測データは初回のまま
		Assert.NotNull(eq.LatestObservationPrefs);
		Assert.Single(eq.LatestObservationPrefs![0].Areas);
	}

	[Fact(DisplayName = "前回状態スナップショットが正しく構築される")]
	public void PreviousState_CorrectlyCaptures()
	{
		var eq = new EarthquakeEvent("test-event");
		var prefs = CreateTestPrefs((350, "東京都23区", JmaIntensity.Int3));
		var data = CreateIntensityData(observationPrefs: prefs);

		eq.ProcessIntermediateData(data);

		// 続報を処理して前回状態を取得
		var data2 = CreateIntensityData(
			observationPrefs: CreateTestPrefs((350, "東京都23区", JmaIntensity.Int4)),
			reportDateTime: new DateTime(2026, 1, 1, 12, 5, 0)
		);
		var result = eq.ProcessIntermediateData(data2);

		Assert.NotNull(result.PreviousState);
		Assert.Equal(JmaIntensity.Int3, result.PreviousState!.Intensity);
		Assert.Null(result.PreviousState.LpgmIntensity);
		Assert.NotNull(result.PreviousState.ObservationPrefs);
		Assert.Null(result.PreviousState.FlatPoints);
	}
}
