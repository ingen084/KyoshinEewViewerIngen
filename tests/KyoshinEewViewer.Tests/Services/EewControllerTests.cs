using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Services;

public class EewControllerTests
{
	[Fact(DisplayName = "擬似キャンセル後_同一報の再受信でキャンセル状態が解除される")]
	public void 擬似キャンセル後_同一報の再受信でキャンセル状態が解除される()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var eew = CreateEew(baseTime);

		controller.Update(eew, baseTime);
		controller.Cancelled(null, baseTime.AddSeconds(1));
		Assert.True(updatedEews.Last().Single().IsCancelled);

		controller.Update(eew with { ReceiveTime = baseTime.AddSeconds(2) }, baseTime.AddSeconds(2));

		var actual = updatedEews.Last().Single();
		Assert.False(actual.IsCancelled);
		Assert.False(actual.IsTrueCancelled);
	}

	[Fact(DisplayName = "確定キャンセル後_同一報の再受信ではキャンセル状態を維持する")]
	public void 確定キャンセル後_同一報の再受信ではキャンセル状態を維持する()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var eew = CreateEew(baseTime);

		controller.Update(eew, baseTime);
		controller.Cancelled(eew.Id, baseTime.AddSeconds(1));
		Assert.True(updatedEews.Last().Single().IsTrueCancelled);

		controller.Update(eew with { ReceiveTime = baseTime.AddSeconds(2) }, baseTime.AddSeconds(2));

		var actual = updatedEews.Last().Single();
		Assert.True(actual.IsCancelled);
		Assert.True(actual.IsTrueCancelled);
	}

	[Fact(DisplayName = "擬似キャンセル後_新しい報数の再受信でキャンセル状態が解除される")]
	public void 擬似キャンセル後_新しい報数の再受信でキャンセル状態が解除される()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var eew = CreateEew(baseTime);

		controller.Update(eew, baseTime);
		controller.Cancelled(null, baseTime.AddSeconds(1));
		Assert.True(updatedEews.Last().Single().IsCancelled);

		controller.Update(eew with { SerialNo = 2, ReceiveTime = baseTime.AddSeconds(2) }, baseTime.AddSeconds(2));

		var actual = updatedEews.Last().Single();
		Assert.Equal(2, actual.SerialNo);
		Assert.False(actual.IsCancelled);
		Assert.False(actual.IsTrueCancelled);
	}

	[Fact(DisplayName = "同一イベントに警報電文がある場合_警報地域のみ警報電文由来を優先する")]
	public void 同一イベントに警報電文がある場合_警報地域のみ警報電文由来を優先する()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var forecastEew = CreateEew(baseTime) with
		{
			DisplaySource = "DM-D.S.S 予報電文",
			MaxIntensity = JmaIntensity.Int4,
			IntensityForecastMap = new()
			{
				[100] = JmaIntensity.Int4,
			},
			Hypocenter = CreateEew(baseTime).Hypocenter! with { Place = "予報電文震源" },
			IsWarning = true,
			WarningAreas = new EewWarningAreas
			{
				DisplaySource = "DM-D.S.S 予報電文",
				SerialNo = 1,
				Codes = [100],
				Names = ["予報電文地域"],
			},
		};
		var warningEew = forecastEew with
		{
			DisplaySource = "DM-D.S.S 警報電文",
			ReceiveTime = baseTime.AddSeconds(1),
			MaxIntensity = JmaIntensity.Int6Upper,
			IsIntensityOver = true,
			IntensityForecastMap = new()
			{
				[200] = JmaIntensity.Int6Upper,
			},
			Hypocenter = forecastEew.Hypocenter! with { Place = "警報電文震源" },
			WarningAreas = new EewWarningAreas
			{
				DisplaySource = "DM-D.S.S 警報電文",
				SerialNo = 1,
				Codes = [200],
				Names = ["警報電文地域"],
				IsWarningTelegram = true,
			},
		};

		controller.Update(forecastEew, baseTime);
		controller.UpdateWarning(warningEew, baseTime.AddSeconds(1));

		var actual = updatedEews.Last().Single();
		Assert.Equal("DM-D.S.S 予報電文", actual.DisplaySource);
		Assert.Equal(JmaIntensity.Int4, actual.MaxIntensity);
		Assert.False(actual.IsIntensityOver);
		Assert.Equal("予報電文震源", actual.Hypocenter?.Place);
		Assert.NotNull(actual.IntensityForecastMap);
		Assert.Equal(JmaIntensity.Int4, actual.IntensityForecastMap[100]);
		Assert.False(actual.IntensityForecastMap.ContainsKey(200));

		var actualWarningAreas = actual.WarningAreas;
		Assert.NotNull(actualWarningAreas);
		Assert.True(actualWarningAreas.IsWarningTelegram);
		Assert.Equal("DM-D.S.S 警報電文", actualWarningAreas.DisplaySource);
		Assert.Equal([200], actualWarningAreas.Codes);
		Assert.Equal(["警報電文地域"], actualWarningAreas.Names);
	}

	private static EewController CreateController()
	{
		var config = new KyoshinEewViewerConfiguration();
		var logManager = new DefaultLogManager();
		return new EewController(
			logManager,
			null!,
			config,
			new SoundPlayerService(config, logManager),
			new WorkflowService(logManager));
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
		};
}
