using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Services;

public class EewPointForecastControllerTests
{
	[Fact(DisplayName = "Webhookのレスポンスから地点予測を取り込みEEWに合成される")]
	public void Webhookのレスポンスから地点予測を取り込みEEWに合成される()
	{
		var (controller, pointForecastController, config) = CreateControllers();
		config.Eew.EnableExternalPointForecast = true;
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = DateTime.Now;
		controller.Update(CreateEew(baseTime), baseTime);

		var result = pointForecastController.InjectFromWebhookResponse(
			"test-eew",
			1,
			$$"""
			{"source":"myscript","displaySource":"自作推定","points":[
				{"name":"自宅","lat":35.681,"lon":139.767,"intensity":4.2,"arrivalAt":"{{baseTime.AddSeconds(30):yyyy-MM-ddTHH:mm:ss}}"},
				{"name":"実家","intensity":2.8}
			]}
			""",
			"workflow-id",
			"ワークフロー名");

		Assert.Contains("2件", result);

		var actual = Assert.Single(updatedEews.Last());
		Assert.NotNull(actual.PointForecasts);
		Assert.Equal(2, actual.PointForecasts.Length);

		// 計測震度の降順に並ぶ
		var home = actual.PointForecasts[0];
		Assert.Equal("自宅", home.PointName);
		Assert.Equal(JmaIntensity.Int4, home.Intensity);
		Assert.Equal("自作推定", home.DisplaySource);
		Assert.NotNull(home.Location);
		Assert.NotNull(home.ArrivalTime);
		// 進捗バーの起点として EEW の発生時刻が引き継がれる
		Assert.Equal(baseTime.AddSeconds(-10), home.OccurrenceTime);

		// 座標と到達時刻が無くても取り込まれる
		var parent = actual.PointForecasts[1];
		Assert.Equal("実家", parent.PointName);
		Assert.Equal(JmaIntensity.Int3, parent.Intensity);
		Assert.Null(parent.Location);
		Assert.Null(parent.ArrivalTime);
	}

	[Fact(DisplayName = "隠し設定が無効な場合は地点予測を取り込まない")]
	public void 隠し設定が無効な場合は地点予測を取り込まない()
	{
		var (controller, pointForecastController, _) = CreateControllers();
		var baseTime = DateTime.Now;
		controller.Update(CreateEew(baseTime), baseTime);

		pointForecastController.InjectFromWebhookResponse(
			"test-eew", 1, """{"points":[{"name":"自宅","intensity":4.2}]}""", "workflow-id", "ワークフロー名");

		Assert.True(controller.TryGetEew("test-eew", out var eew));
		Assert.Null(eew?.PointForecasts);
	}

	[Fact(DisplayName = "不正な値の地点予測は破棄される")]
	public void 不正な値の地点予測は破棄される()
	{
		var (controller, pointForecastController, config) = CreateControllers();
		config.Eew.EnableExternalPointForecast = true;
		var baseTime = DateTime.Now;
		controller.Update(CreateEew(baseTime), baseTime);

		var result = pointForecastController.InjectFromWebhookResponse(
			"test-eew",
			1,
			"""
			{"points":[
				{"name":"計測震度なし"},
				{"name":"範囲外","intensity":99},
				{"name":"","intensity":3.0},
				{"name":"正常","intensity":3.0}
			]}
			""",
			"workflow-id",
			"ワークフロー名");

		Assert.Contains("1件", result);
		Assert.Contains("破棄 3件", result);
	}

	[Fact(DisplayName = "地点予測が空配列の場合はその提供元の地点予測を削除する")]
	public void 地点予測が空配列の場合はその提供元の地点予測を削除する()
	{
		var (controller, pointForecastController, config) = CreateControllers();
		config.Eew.EnableExternalPointForecast = true;
		var baseTime = DateTime.Now;
		controller.Update(CreateEew(baseTime), baseTime);

		pointForecastController.InjectFromWebhookResponse(
			"test-eew", 1, """{"source":"myscript","points":[{"name":"自宅","intensity":4.2}]}""", "workflow-id", "ワークフロー名");
		Assert.True(controller.TryGetEew("test-eew", out _));

		pointForecastController.InjectFromWebhookResponse(
			"test-eew", 2, """{"source":"myscript","points":[]}""", "workflow-id", "ワークフロー名");

		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		controller.RefreshEewUpdated(baseTime);
		Assert.Null(Assert.Single(updatedEews.Last()).PointForecasts);
	}

	[Fact(DisplayName = "リプレイ中の過去時刻を基準に到達時刻を受け付ける")]
	public void リプレイ中の過去時刻を基準に到達時刻を受け付ける()
	{
		// リプレイ中のホストは過去の時刻を現在時刻として返す
		var replayTime = new DateTime(2021, 2, 13, 23, 8, 0);
		var (controller, pointForecastController, config) = CreateControllers(() => replayTime);
		config.Eew.EnableExternalPointForecast = true;
		controller.Update(CreateEew(replayTime), replayTime);

		var result = pointForecastController.InjectFromWebhookResponse(
			"test-eew",
			1,
			$$"""
			{"points":[{"name":"自宅","intensity":4.2,"arrivalAt":"{{replayTime.AddSeconds(30):yyyy-MM-ddTHH:mm:ss}}"}]}
			""",
			"workflow-id",
			"ワークフロー名");

		Assert.Contains("1件", result);

		Assert.True(controller.TryGetEew("test-eew", out _));
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		controller.RefreshEewUpdated(replayTime);

		var forecast = Assert.Single(Assert.Single(updatedEews.Last()).PointForecasts!);
		// 実時間から見ると大きく過去の到達時刻でも、リプレイ時刻基準で受け付けられる
		Assert.Equal(replayTime.AddSeconds(30), forecast.ArrivalTime);
		Assert.Equal(replayTime, forecast.ReceiveTime);
	}

	[Fact(DisplayName = "進捗バーは地震発生時刻を起点に計算される")]
	public void 進捗バーは地震発生時刻を起点に計算される()
	{
		var occurrence = new DateTime(2026, 7, 25, 12, 0, 0);
		// 第2報を発生から10秒後に受信し、発生から30秒後に到達する想定
		var forecast = new EewPointForecast
		{
			EventId = "test-eew",
			SourceKey = "myscript",
			DisplaySource = "自作推定",
			SerialNo = 2,
			PointName = "自宅",
			RealtimeIntensity = 4.0,
			OccurrenceTime = occurrence,
			ArrivalTime = occurrence.AddSeconds(30),
			ReceiveTime = occurrence.AddSeconds(10),
		};

		// 発生から15秒後: 発生起点なら50% (受信起点だと25%になり、報の更新のたびにバーが戻ってしまう)
		forecast.UpdateDisplayValues(occurrence.AddSeconds(15), false);
		Assert.Equal(50, forecast.ProgressPercent, 3);

		// 発生時刻が不明な場合は従来どおり受信時点を起点にする
		var withoutOccurrence = new EewPointForecast
		{
			EventId = "test-eew",
			SourceKey = "myscript",
			DisplaySource = "自作推定",
			SerialNo = 2,
			PointName = "自宅",
			RealtimeIntensity = 4.0,
			OccurrenceTime = null,
			ArrivalTime = occurrence.AddSeconds(30),
			ReceiveTime = occurrence.AddSeconds(10),
		};
		withoutOccurrence.UpdateDisplayValues(occurrence.AddSeconds(15), false);
		Assert.Equal(25, withoutOccurrence.ProgressPercent, 3);
	}

	[Fact(DisplayName = "存在しないEEWの地点予測は取り込まない")]
	public void 存在しないEEWの地点予測は取り込まない()
	{
		var (_, pointForecastController, config) = CreateControllers();
		config.Eew.EnableExternalPointForecast = true;

		var result = pointForecastController.InjectFromWebhookResponse(
			"unknown-eew", 1, """{"points":[{"name":"自宅","intensity":4.2}]}""", "workflow-id", "ワークフロー名");

		Assert.Contains("対象のEEWが存在しません", result);
	}

	[Fact(DisplayName = "キャンセルされたEEWには地点予測を合成しない")]
	public void キャンセルされたEEWには地点予測を合成しない()
	{
		var (controller, pointForecastController, config) = CreateControllers();
		config.Eew.EnableExternalPointForecast = true;
		var baseTime = DateTime.Now;
		controller.Update(CreateEew(baseTime), baseTime);
		pointForecastController.InjectFromWebhookResponse(
			"test-eew", 1, """{"points":[{"name":"自宅","intensity":4.2}]}""", "workflow-id", "ワークフロー名");

		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		controller.Cancelled("test-eew", baseTime.AddSeconds(1));

		var actual = Assert.Single(updatedEews.Last());
		Assert.True(actual.IsCancelled);
		Assert.Null(actual.PointForecasts);
	}

	private static (EewController, EewPointForecastController, KyoshinEewViewerConfiguration) CreateControllers(Func<DateTime>? getCurrentTime = null)
	{
		var config = new KyoshinEewViewerConfiguration();
		var logManager = new DefaultLogManager();
		var timerService = new TimerService(logManager, config);
		var eewController = new EewController(
			logManager,
			null!,
			config,
			new SoundPlayerService(config, logManager),
			new WorkflowService(logManager));
		var pointForecastController = new EewPointForecastController(logManager, config, eewController, timerService, getCurrentTime);
		return (eewController, pointForecastController, config);
	}

	private static Eew CreateEew(DateTime receiveTime)
		=> EewMock.NORMAL with
		{
			Id = "test-eew",
			DisplaySource = "強震モニタ",
			ReceiveTime = receiveTime,
			SerialNo = 1,
			IsCancelled = false,
			IsTrueCancelled = false,
			Source = EewSource.KyoshinMonitor,
			Hypocenter = EewMock.NORMAL.Hypocenter! with { OccurrenceTime = receiveTime.AddSeconds(-10) },
		};
}
