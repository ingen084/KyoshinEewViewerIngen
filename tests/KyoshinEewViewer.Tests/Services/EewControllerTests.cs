using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyoshinEewViewer.Tests.Services;

public class EewControllerTests
{
	[Fact(DisplayName = "音声設定に緊急地震速報警報発表時の項目を登録する")]
	public void 音声設定に緊急地震速報警報発表時の項目を登録する()
	{
		var config = new KyoshinEewViewerConfiguration();
		var soundPlayer = new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance);
		_ = CreateController(config, soundPlayer);

		var sounds = soundPlayer.RegisteredSounds[new SoundCategory("Eew", "緊急地震速報")];
		var sound = Assert.Single(sounds, s => s.Name == "EewWarningAnnounced");
		Assert.Equal("緊急地震速報(警報)発表･レベル到達時", sound.DisplayName);
		Assert.Equal("5-", sound.ExampleParameter?["int"]);
	}

	[Fact(DisplayName = "予報受信後に警報電文を受信しても警報音を重複して処理しない")]
	public void 予報受信後に警報電文を受信しても警報音を重複して処理しない()
	{
		var config = new KyoshinEewViewerConfiguration();
		var soundPlayer = new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance);
		var controller = CreateController(config, soundPlayer);
		var baseTime = new DateTime(2026, 8, 23, 12, 0, 0);

		controller.Update(CreateEew(baseTime), baseTime);
		controller.UpdateWarning(CreateWarningEew(baseTime.AddSeconds(1)), baseTime.AddSeconds(1));

		var soundConfigs = config.Sounds["Eew"];
		Assert.True(soundConfigs.Remove("EewWarningAnnounced"));

		controller.Update(
			CreateEew(baseTime.AddSeconds(2)) with { SerialNo = 2, IsWarning = true },
			baseTime.AddSeconds(2));

		Assert.DoesNotContain("EewWarningAnnounced", soundConfigs);
	}

	[Fact(DisplayName = "警報状態の予報受信後に警報電文を受信しても警報音を重複して処理しない")]
	public void 警報状態の予報受信後に警報電文を受信しても警報音を重複して処理しない()
	{
		var config = new KyoshinEewViewerConfiguration();
		var soundPlayer = new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance);
		var controller = CreateController(config, soundPlayer);
		var baseTime = new DateTime(2026, 8, 23, 12, 0, 0);

		controller.Update(CreateEew(baseTime) with { IsWarning = true }, baseTime);

		var soundConfigs = config.Sounds["Eew"];
		Assert.True(soundConfigs.Remove("EewWarningAnnounced"));

		controller.UpdateWarning(CreateWarningEew(baseTime.AddSeconds(1)), baseTime.AddSeconds(1));

		Assert.DoesNotContain("EewWarningAnnounced", soundConfigs);
	}

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

	[Fact(DisplayName = "擬似キャンセル後_確定キャンセルを受信すると通知を繰り返さず確定キャンセルに切り替わる")]
	public void 擬似キャンセル後_確定キャンセルを受信すると通知を繰り返さず確定キャンセルに切り替わる()
	{
		var config = new KyoshinEewViewerConfiguration();
		var soundPlayer = new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance);
		var controller = CreateController(config, soundPlayer);
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var eew = CreateEew(baseTime);

		controller.Update(eew, baseTime);
		controller.Cancelled(null, baseTime.AddSeconds(1));
		Assert.False(updatedEews.Last().Single().IsTrueCancelled);

		var soundConfigs = config.Sounds["Eew"];
		Assert.True(soundConfigs.Remove("EewCanceled"));
		Assert.True(soundConfigs.Remove("EewReceived"));

		controller.Cancelled(eew.Id, baseTime.AddSeconds(2));

		var cancelled = updatedEews.Last().Single();
		Assert.True(cancelled.IsCancelled);
		Assert.True(cancelled.IsTrueCancelled);
		Assert.DoesNotContain("EewCanceled", soundConfigs);
		Assert.DoesNotContain("EewReceived", soundConfigs);

		// 確定キャンセル後は同一報を再受信しても擬似キャンセルのように解除されない
		controller.Update(eew with { ReceiveTime = baseTime.AddSeconds(3) }, baseTime.AddSeconds(3));

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

	[Fact(DisplayName = "同じ報数の予報電文と EEW Client の予想震度・警報地域を補い合い、塗りつぶしは設定に応じて選ばれ、報数が進むと引き継がない")]
	public void 同じ報数の予報電文とEEWClientの予想震度を補い合う()
	{
		var config = new KyoshinEewViewerConfiguration();
		var controller = CreateController(config, new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance));
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 9, 24, 12, 0, 0);

		var telegramEew = CreateEew(baseTime) with
		{
			Source = EewSource.Dmdata,
			DisplaySource = "DM-D.S.S 予報電文",
			IntensityForecastMap = new() { [100] = JmaIntensity.Int4 },
			WarningAreas = new EewWarningAreas
			{
				DisplaySource = "DM-D.S.S 予報電文",
				SerialNo = 1,
				Codes = [100],
				Names = ["予報電文地域"],
			},
		};
		var eewClientEew = CreateEew(baseTime.AddSeconds(1)) with
		{
			Source = EewSource.DmdataEewClient,
			DisplaySource = "EEW Client",
			CalculatedIntensityForecastMap = new() { [100] = JmaIntensity.Int5Lower, [200] = JmaIntensity.Int1 },
			CalculatedIntensityForecastSource = "EEW Client",
		};

		// どちらが先に届いても、先に受信した方の内容に他方の予想震度が補われる
		foreach (var (first, second) in new[] { (telegramEew, eewClientEew), (eewClientEew, telegramEew) })
		{
			controller.Clear();
			controller.Update(first, first.ReceiveTime);
			controller.Update(second, second.ReceiveTime);

			var merged = updatedEews.Last().Single();
			Assert.Equal(first.DisplaySource, merged.DisplaySource);
			Assert.Equal(JmaIntensity.Int4, merged.IntensityForecastMap?[100]);
			Assert.Equal(JmaIntensity.Int5Lower, merged.CalculatedIntensityForecastMap?[100]);
			Assert.Equal("EEW Client", merged.CalculatedIntensityForecastSource);
			// EEW Client には含まれない警報地域も予報電文から補われる
			Assert.Equal(["予報電文地域"], merged.WarningAreas!.Names);

			// 既定では気象庁の予想震度で塗りつぶす
			Assert.False(merged.IsFillIntensityForecastCalculated);
			Assert.Same(merged.IntensityForecastMap, merged.FillIntensityForecastMap);
		}

		// 優先する設定にすると計算値で塗りつぶす
		config.Eew.PreferCalculatedIntensityForecast = true;
		controller.RefreshEewUpdated(baseTime.AddSeconds(2));
		var preferred = updatedEews.Last().Single();
		Assert.True(preferred.IsFillIntensityForecastCalculated);
		Assert.Same(preferred.CalculatedIntensityForecastMap, preferred.FillIntensityForecastMap);

		// 報数が進むと震源が変わりうるため、前の報数の予想震度は引き継がない
		config.Eew.PreferCalculatedIntensityForecast = false;
		controller.Update(telegramEew with { SerialNo = 2, ReceiveTime = baseTime.AddSeconds(3) }, baseTime.AddSeconds(3));
		var next = updatedEews.Last().Single();
		Assert.Null(next.CalculatedIntensityForecastMap);

		// 気象庁の予想震度が無い場合は設定によらず計算値で塗りつぶす
		controller.Update(eewClientEew with { SerialNo = 3, ReceiveTime = baseTime.AddSeconds(4) }, baseTime.AddSeconds(4));
		var calculatedOnly = updatedEews.Last().Single();
		Assert.Null(calculatedOnly.IntensityForecastMap);
		Assert.True(calculatedOnly.IsFillIntensityForecastCalculated);
	}

	[Fact(DisplayName = "EEW Client の同じ報数の再配信で計算値が更新され、他の受信元から補った予想震度と警報地域は残る")]
	public void EEWClientの同じ報数の再配信で計算値が更新される()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 9, 24, 12, 0, 0);

		var telegramEew = CreateEew(baseTime) with
		{
			Source = EewSource.Dmdata,
			DisplaySource = "DM-D.S.S 予報電文",
			IntensityForecastMap = new() { [100] = JmaIntensity.Int4 },
			WarningAreas = new EewWarningAreas { DisplaySource = "DM-D.S.S 予報電文", SerialNo = 1, Codes = [100], Names = ["予報電文地域"] },
		};
		var eewClientEew = CreateEew(baseTime) with
		{
			Source = EewSource.DmdataEewClient,
			DisplaySource = "EEW Client",
			CalculatedIntensityForecastMap = new() { [100] = JmaIntensity.Int3 },
			CalculatedIntensityForecastSource = "EEW Client",
		};
		var redelivered = eewClientEew with
		{
			CalculatedIntensityForecastMap = new() { [100] = JmaIntensity.Int5Lower },
			ReceiveTime = baseTime.AddSeconds(2),
		};

		foreach (var (first, second) in new[] { (telegramEew, eewClientEew), (eewClientEew, telegramEew) })
		{
			controller.Clear();
			controller.Update(first, baseTime);
			controller.Update(second, baseTime.AddSeconds(1));
			controller.Update(redelivered, baseTime.AddSeconds(2));

			var merged = updatedEews.Last().Single();
			Assert.Equal(first.Source, merged.Source);
			Assert.Equal(JmaIntensity.Int5Lower, merged.CalculatedIntensityForecastMap?[100]);
			Assert.Equal(JmaIntensity.Int4, merged.IntensityForecastMap?[100]);
			Assert.Equal(["予報電文地域"], merged.WarningAreas!.Names);
		}
	}

	[Fact(DisplayName = "SNP の警報地域は EEW Client と同じ報数で統合しても受信順によらず失われない")]
	public void SNPの警報地域はEEWClientと統合しても失われない()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 9, 24, 12, 0, 0);

		var snpEew = CreateEew(baseTime) with
		{
			Source = EewSource.SignalNowProfessional,
			DisplaySource = "SignalNowProfessional",
			WarningAreas = new EewWarningAreas { DisplaySource = "SNP", SerialNo = 1, Codes = [100], Names = ["SNP地域"] },
		};
		var eewClientEew = CreateEew(baseTime.AddSeconds(1)) with
		{
			Source = EewSource.DmdataEewClient,
			DisplaySource = "EEW Client",
		};

		foreach (var (first, second) in new[] { (snpEew, eewClientEew), (eewClientEew, snpEew) })
		{
			controller.Clear();
			controller.Update(first, baseTime);
			controller.Update(second, baseTime.AddSeconds(1));

			var merged = updatedEews.Last().Single();
			Assert.Equal(EewSource.DmdataEewClient, merged.Source);
			Assert.Equal(["SNP地域"], merged.WarningAreas!.Names);
		}
	}

	[Fact(DisplayName = "計算値が空の場合は計算値として扱わず、気象庁の予想震度で塗りつぶす")]
	public void 計算値が空の場合は計算値として扱わない()
	{
		var jmaForecast = new Dictionary<int, JmaIntensity> { [100] = JmaIntensity.Int4 };
		var eew = CreateEew(new DateTime(2026, 9, 24, 12, 0, 0)) with
		{
			CalculatedIntensityForecastMap = [],
			CalculatedIntensityForecastSource = "EEW Client",
			PreferCalculatedIntensityForecast = true,
		};

		Assert.False(eew.IsFillIntensityForecastCalculated);
		Assert.Null(eew.FillIntensityForecastMap);

		var withJmaForecast = eew with { IntensityForecastMap = jmaForecast };
		Assert.False(withJmaForecast.IsFillIntensityForecastCalculated);
		Assert.Same(jmaForecast, withJmaForecast.FillIntensityForecastMap);
	}

	[Fact(DisplayName = "EEWの期限切れ処理でも警報のみのEEWは通知に含まれる")]
	public void EEWの期限切れ処理でも警報のみのEEWは通知に含まれる()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);

		// 期限切れになる通常のEEWと、まだ有効な警報のみのEEWを登録する
		controller.Update(CreateEew(baseTime), baseTime);
		var warningOnlyEew = CreateEew(baseTime.AddMinutes(2)) with
		{
			Id = "test-warning-only",
			IsWarning = true,
			WarningAreas = new EewWarningAreas
			{
				DisplaySource = "DM-D.S.S 警報電文",
				SerialNo = 1,
				Codes = [200],
				Names = ["警報電文地域"],
				IsWarningTelegram = true,
			},
		};
		controller.UpdateWarning(warningOnlyEew, baseTime.AddMinutes(2));

		// 通常のEEWのみが期限切れになるタイミング
		controller.TimerElapsed(baseTime.AddMinutes(3).AddSeconds(1));

		var actual = updatedEews.Last();
		Assert.Equal("test-warning-only", Assert.Single(actual).Id);
	}

	[Fact(DisplayName = "予報電文が残っている間は警報電文を期限切れで削除しない")]
	public void 予報電文が残っている間は警報電文を期限切れで削除しない()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);

		// 警報電文の受信後も予報電文の更新が続き、予報電文のほうが新しい状態
		controller.Update(CreateEew(baseTime), baseTime);
		controller.UpdateWarning(CreateWarningEew(baseTime.AddSeconds(5)), baseTime.AddSeconds(5));
		controller.Update(CreateEew(baseTime.AddMinutes(1)) with { SerialNo = 2 }, baseTime.AddMinutes(1));

		// 警報電文の受信から3分経過したが、予報電文はまだ期限内
		controller.TimerElapsed(baseTime.AddMinutes(3).AddSeconds(5));
		var actual = Assert.Single(updatedEews.Last());
		Assert.Equal("test-eew", actual.Id);
		Assert.NotNull(actual.WarningAreas);

		// 予報電文が期限切れになれば警報電文も連動して削除される
		controller.TimerElapsed(baseTime.AddMinutes(4).AddSeconds(5));
		Assert.Empty(updatedEews.Last());
	}

	[Fact(DisplayName = "訓練以外の EEW を受信すると、表示されない精度の低い EEW や警報電文でも訓練の EEW を終了する")]
	public void 訓練以外のEEWを受信すると訓練のEEWを終了する()
	{
		var baseTime = new DateTime(2026, 11, 8, 12, 0, 0);
		var lowAccuracy = CreateEew(baseTime) with
		{
			Hypocenter = EewMock.NORMAL.Hypocenter! with { Accuracy = new() { IsLocked = false, LocationAccuracy = 1, DepthAccuracy = 1, MagnitudeAccuracy = 0 } },
		};
		(string Name, Action<EewController> Receive)[] cases =
		[
			("予報", c => c.Update(CreateEew(baseTime), baseTime)),
			("警報電文", c => c.UpdateWarning(CreateWarningEew(baseTime), baseTime)),
			("精度が低く表示されない予報", c => c.Update(lowAccuracy, baseTime)),
		];

		foreach (var (name, receive) in cases)
		{
			var controller = CreateController();
			var updatedEews = new List<Eew[]>();
			controller.EewUpdated += (_, eews) => updatedEews.Add(eews);

			controller.Update(CreateEew(baseTime) with { IsTraining = true }, baseTime);
			controller.UpdateWarning(CreateWarningEew(baseTime) with { IsTraining = true }, baseTime);
			Assert.True(Assert.Single(updatedEews.Last()).IsTraining, name);

			receive(controller);
			Assert.DoesNotContain(updatedEews.Last(), e => e.IsTraining);
			Assert.False(controller.TryGetEew(Eew.GetKey("test-eew", true), out _), name);
		}
	}

	[Fact(DisplayName = "訓練以外の EEW の表示中は訓練の EEW を受け付けず、表示中の訓練の EEW は即時終了できる")]
	public void 訓練以外のEEWの表示中は訓練のEEWを受け付けない()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 11, 8, 12, 0, 0);
		var training = CreateEew(baseTime) with { Id = "training-eew", IsTraining = true };

		Assert.False(controller.EndTraining(baseTime));
		controller.Update(training, baseTime);
		Assert.True(controller.EndTraining(baseTime.AddSeconds(1)));
		Assert.Empty(updatedEews.Last());

		controller.Update(CreateEew(baseTime.AddSeconds(2)), baseTime.AddSeconds(2));
		var updateCount = updatedEews.Count;
		controller.Update(training with { SerialNo = 2 }, baseTime.AddSeconds(3));
		controller.UpdateWarning(CreateWarningEew(baseTime.AddSeconds(3)) with { Id = "training-eew", IsTraining = true }, baseTime.AddSeconds(3));
		Assert.Equal(updateCount, updatedEews.Count);
		Assert.False(controller.TryGetEew(training.Key, out _));
		Assert.False(Assert.Single(updatedEews.Last()).IsTraining);
	}

	private static Eew CreateWarningEew(DateTime receiveTime)
		=> CreateEew(receiveTime) with
		{
			DisplaySource = "DM-D.S.S 警報電文",
			IsWarning = true,
			WarningAreas = new EewWarningAreas
			{
				DisplaySource = "DM-D.S.S 警報電文",
				SerialNo = 1,
				Codes = [200],
				Names = ["警報電文地域"],
				IsWarningTelegram = true,
			},
		};

	private static EewController CreateController()
	{
		var config = new KyoshinEewViewerConfiguration();
		return CreateController(config, new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance));
	}

	private static EewController CreateController(KyoshinEewViewerConfiguration config, SoundPlayerService soundPlayer)
		=> new(
			NullLogger<EewController>.Instance,
			null!,
			config,
			soundPlayer,
			new WorkflowService(NullLogger<WorkflowService>.Instance));

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
