using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Splat;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
public class EewTelegramSubscriber : ObservableObject
{
	private ILogger Logger { get; }
	private EewController EewController { get; }
	private TimerService Timer { get; }

	private bool _enabled;
	public bool Enabled
	{
		get => _enabled;
		set => SetProperty(ref _enabled, value);
	}

	private bool _warningOnlyEnabled;
	public bool WarningOnlyEnabled
	{
		get => _warningOnlyEnabled;
		set => SetProperty(ref _warningOnlyEnabled, value);
	}

	private bool _disconnected = true;
	public bool IsDisconnected
	{
		get => _disconnected;
		set => SetProperty(ref _disconnected, value);
	}

	public EewTelegramSubscriber(ILogManager logManager, EewController eewControlService, TelegramProvideService telegramProvider, TimerService timer)
	{
		Logger = logManager.GetLogger<EewTelegramSubscriber>();
		EewController = eewControlService;
		Timer = timer;

		telegramProvider.Subscribe(
			InformationCategory.EewForecast,
			(s, t) =>
			{
				// 有効になった
				Enabled = true;
				IsDisconnected = false;
				return Task.CompletedTask;
			},
			async t =>
			{
				var sw = Stopwatch.StartNew();
				// 受信した
				try
				{
					await using var stream = await t.GetBodyAsync();
					using var report = new JmaXmlDocument(stream);

					// サポート外であれば見なかったことにする
					if (report.Control.Title == "緊急地震速報配信テスト")
					{
						Logger.LogInfo($"dmdataから緊急地震速報のテスト電文を受信しました: {report.Head.EventId} / {report.Control.EditorialOffice}");
						return;
					}

					// 訓練･試験は今のところ非対応
					if (report.Control.Status != "通常")
						return;

					// 今のところ予報電文のみ対応
					if (report.Control.Title != "緊急地震速報（地震動予報）")
					{
						if (report.Control.Title != "緊急地震速報（予報）")
							Logger.LogWarning($"dmdataからEEW予報以外の電文を受信しました: {report.Control.Title}");
						return;
					}

					// 取消報
					if (report.Head.InfoType == "取消")
					{
						Logger.LogInfo($"dmdataからEEW取消報を受信しました: {report.Head.EventId}");
						EewController.Cancelled(report.Head.EventId, Timer.CurrentTime);
						return;
					}
					Logger.LogInfo($"dmdataからEEWを受信しました: {report.Head.EventId}");

					var earthquake = report.EarthquakeBody.Earthquake ?? throw new Exception("Earthquake 要素が見つかりません");
					var warningAreas = report.EarthquakeBody.Intensity?.Forecast?.Prefs.SelectMany(p => p.Areas.Where(a => a.Category?.Kind.Code is "10" or "11" or "19")).ToArray();
					var eew = new Models.Eew
					{
						Id = report.Head.EventId,
						Source = EewSource.Dmdata,
						DisplaySource = $"DM-D.S.S({report.Control.EditorialOffice})",
						ReceiveTime = Timer.CurrentTime,
						SerialNo = int.Parse(report.Head.Serial),
						IsFinal = report.EarthquakeBody.NextAdvisory == "この情報をもって、緊急地震速報：最終報とします。",
						MaxIntensity = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntFrom.ToJmaIntensity() ?? JmaIntensity.Unknown,
						IsIntensityOver = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntTo == "over",
						// TODO LPGM
						Hypocenter = new EewHypocenter
						{
							OccurrenceTime = earthquake.OriginTime?.DateTime ?? report.EarthquakeBody.Earthquake?.ArrivalTime?.DateTime ?? throw new Exception("OccurrenceTime が取得できません"),
							Place = earthquake.Hypocenter.Area.Name,
							Location = CoordinateConverter.GetLocation(earthquake.Hypocenter.Area.Coordinate.Value),
							Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? (float.IsNaN(m) ? null : m) : null,
							Depth = CoordinateConverter.GetDepth(earthquake.Hypocenter.Area.Coordinate.Value) ?? -1,
							IsTemporary = earthquake.Condition == "仮定震源要素",
							Accuracy = new EewHypocenterAccuracy
							{
								IsLocked = earthquake.Hypocenter.Accuracy.EpicenterRank2 == 9,
								LocationAccuracy = earthquake.Hypocenter.Accuracy.EpicenterRank,
								DepthAccuracy = earthquake.Hypocenter.Accuracy.DepthRank,
								MagnitudeAccuracy = earthquake.Hypocenter.Accuracy.MagnitudeCalculationRank,
							},
						},
						IntensityForecastMap = report.EarthquakeBody.Intensity?.Forecast?.Prefs
							.SelectMany(p => p.Areas.Select(a => (a.Code, a.ForecastIntTo == "over" ? a.ForecastIntFrom.ToJmaIntensity() : a.ForecastIntTo.ToJmaIntensity())))
							.Where(a => a.Item2 != JmaIntensity.Unknown)
							.ToDictionary(k => k.Code, v => v.Item2),
						WarningAreas = (warningAreas?.Any() ?? false) ? new EewWarningAreas
						{
							DisplaySource = "DM-D.S.S 予報電文",
							SerialNo = int.Parse(report.Head.Serial),
							Codes = warningAreas?.Select(a => a.Code).ToArray() ?? [],
							Names = EewAreaGroups.Compressor.Compress(warningAreas?.Select(a => a.Name).ToArray() ?? []),
						} : null,
						IsWarning = report.EarthquakeBody.Comments?.WarningCommentCode?.Contains("0201") ?? false,
					};

					EewController.Update(eew, t.ArrivalTime);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "EEW電文処理中に例外が発生しました");
				}
				finally
				{
					Logger.LogDebug($"dmdataEEW 処理時間: {sw.Elapsed.TotalMilliseconds:0.000}ms");
				}
			},
			s =>
			{
				// 死んだ
				Enabled = !s.isAllFailed;
				IsDisconnected = s.isRestorable;
			});
		telegramProvider.Subscribe(
			InformationCategory.EewWarning,
			(s, t) =>
			{
				WarningOnlyEnabled = !Enabled;
				IsDisconnected = false;
				return Task.CompletedTask;
			},
			async t =>
			{
				var sw = Stopwatch.StartNew();
				// 受信した
				try
				{
					await using var stream = await t.GetBodyAsync();
					using var report = new JmaXmlDocument(stream);

					// 訓練･試験は今のところ非対応
					if (report.Control.Status != "通常")
						return;

					// 今のところ予報電文のみ対応
					if (report.Control.Title != "緊急地震速報（警報）")
					{
						Logger.LogWarning($"dmdataからEEW警報以外の電文を受信しました: {report.Control.Title}");
						return;
					}

					// 取消報
					if (report.Head.InfoType == "取消")
					{
						Logger.LogInfo($"dmdataからEEW警報の取消報を受信しました: {report.Head.EventId}");
						EewController.WarningCancelled(report.Head.EventId, Timer.CurrentTime);
						return;
					}
					Logger.LogInfo($"dmdataからEEW警報を受信しました: {report.Head.EventId}");

					var earthquake = report.EarthquakeBody.Earthquake ?? throw new Exception("Earthquake 要素が見つかりません");
					var warningAreas = report.EarthquakeBody.Intensity?.Forecast?.Prefs.SelectMany(p => p.Areas.Where(a => a.Category?.Kind.Code is "10" or "11" or "19")).ToArray();
					EewController.UpdateWarning(new Models.Eew
					{
						Id = report.Head.EventId,
						Source = EewSource.Dmdata,
						DisplaySource = $"DM-D.S.S({report.Control.EditorialOffice}) 警報電文",
						ReceiveTime = Timer.CurrentTime,
						SerialNo = int.Parse(report.Head.Serial),
						IsFinal = report.EarthquakeBody.NextAdvisory == "この情報をもって、緊急地震速報：最終報とします。",
						MaxIntensity = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntFrom.ToJmaIntensity() ?? JmaIntensity.Unknown,
						IsIntensityOver = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntTo == "over",
						Hypocenter = new EewHypocenter
						{
							OccurrenceTime = earthquake.OriginTime?.DateTime ?? report.EarthquakeBody.Earthquake?.ArrivalTime?.DateTime ?? throw new Exception("OccurrenceTime が取得できません"),
							Place = earthquake.Hypocenter.Area.Name,
							Location = CoordinateConverter.GetLocation(earthquake.Hypocenter.Area.Coordinate.Value),
							Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? (float.IsNaN(m) ? null : m) : null,
							Depth = CoordinateConverter.GetDepth(earthquake.Hypocenter.Area.Coordinate.Value) ?? -1,
							IsTemporary = earthquake.Condition == "仮定震源要素",
						},

						IsWarning = true,
						WarningAreas = new EewWarningAreas
						{
							DisplaySource = "DM-D.S.S 警報電文",
							SerialNo = int.Parse(report.Head.Serial),
							Codes = warningAreas?.Select(a => a.Code).ToArray() ?? [],
							Names = EewAreaGroups.Compressor.Compress(warningAreas?.Select(a => a.Name).ToArray() ?? []),
							IsWarningTelegram = true,
						},
					}, t.ArrivalTime);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "EEW警報電文処理中に例外が発生しました");
				}
				finally
				{
					Logger.LogDebug($"dmdataEEW 処理時間: {sw.Elapsed.TotalMilliseconds:0.000}ms");
				}
			},
			s =>
			{
				// 死んだ
				WarningOnlyEnabled = !s.isAllFailed && !Enabled;
				IsDisconnected = s.isRestorable;
			});

		if (Design.IsDesignMode)
			Enabled = true;
	}
}
