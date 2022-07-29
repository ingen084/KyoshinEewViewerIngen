using Avalonia.Controls;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
public class DmdataEewTelegramService : ReactiveObject
{
	private ILogger Logger { get; }
	private EewController EewController { get; }

	private bool _enabled;
	public bool Enabled
	{
		get => _enabled;
		set => this.RaiseAndSetIfChanged(ref _enabled, value);
	}

	public DmdataEewTelegramService(EewController eewControlService, TelegramProvideService telegramProvider)
	{
		EewController = eewControlService;
		Logger = LoggingService.CreateLogger(this);

		if (Design.IsDesignMode)
		{
			Enabled = true;
			return;
		}

		telegramProvider.Subscribe(
			InformationCategory.EewForecast,
			(s, t) =>
			{
				// 有効になった
				Enabled = true;
			},
			async t =>
			{
				var sw = Stopwatch.StartNew();
				// 受信した
				try
				{
					using var stream = await t.GetBodyAsync();
					using var report = new JmaXmlDocument(stream);

					// サポート外であれば見なかったことにする
					if (report.Control.Title == "緊急地震速報配信テスト")
					{
						Logger.LogInformation("dmdataから緊急地震速報のテスト電文を受信しました");
						return;
					}

					// 訓練･試験は今のところ非対応
					if (report.Control.Status != "通常")
						return;

					// 今のところ予報電文のみ対応
					if (report.Control.Title != "緊急地震速報（予報）")
					{
						Logger.LogWarning("dmdataからEEW予報以外の電文を受信しました: {title}", report.Control.Title);
						return;
					}

					// 取消報
					if (report.Head.InfoType == "取消")
					{
						Logger.LogInformation("dmdataからEEW取消報を受信しました: {eventId}", report.Head.EventId);
						EewController.UpdateOrRefreshEew(
							new DmdataEew(report.Head.EventId, $"DM-D.S.S ({report.Control.EditorialOffice})", true, t.ArrivalTime)
							{
								Count = int.TryParse(report.Head.Serial, out var c2) ? c2 : -1,
							},
							t.ArrivalTime);
						return;
					}
					Logger.LogInformation("dmdataからEEWを受信しました: {eventId}", report.Head.EventId);

					var earthquake = report.EarthquakeBody.Earthquake ?? throw new Exception("Earthquake 要素が見つかりません");

					EewController.UpdateOrRefreshEew(
						new DmdataEew(report.Head.EventId, $"DM-D.S.S ({report.Control.EditorialOffice})", false, t.ArrivalTime) 
						{
							Count = int.TryParse(report.Head.Serial, out var c) ? c : -1,
							IsTemporaryEpicenter = earthquake.Condition == "仮定震源要素",
							OccurrenceTime = earthquake.OriginTime?.DateTime ?? report.EarthquakeBody.Earthquake?.ArrivalTime?.DateTime ?? throw new Exception("OccurrenceTime が取得できません"),
							Place = earthquake.Hypocenter.Area.Name,
							Location = CoordinateConverter.GetLocation(earthquake.Hypocenter.Area.Coordinate.Value),
							Depth = CoordinateConverter.GetDepth(earthquake.Hypocenter.Area.Coordinate.Value) ?? -1,
							LocationAccuracy = earthquake.Hypocenter.Accuracy.EpicenterRank,
							DepthAccuracy = earthquake.Hypocenter.Accuracy.DepthRank,
							MagnitudeAccuracy = earthquake.Hypocenter.Accuracy.MagnitudeCalculationRank,
							Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? (float.IsNaN(m) ? null : m) : null,
							Intensity = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntFrom.ToJmaIntensity() ?? JmaIntensity.Unknown, // TODO 以上 に対応
							IsAccuracyFound = true,
							IsLocked = earthquake.Hypocenter.Accuracy.EpicenterRank2 == 9,
							IsFinal = report.EarthquakeBody.NextAdvisory == "この情報をもって、緊急地震速報：最終報とします。",
							IsWarning = report.EarthquakeBody.Comments?.WarningCommentCode?.Contains("0201") ?? false,
						},
						t.ArrivalTime);
				}
				catch (Exception ex)
				{
					Logger.LogError("EEW電文処理中に例外が発生しました: {ex}", ex);
				}
				finally
				{
					Logger.LogDebug("dmdataEEW 処理時間: {time}ms", sw.Elapsed.TotalMilliseconds.ToString("0.000"));
				}
			},
			isAllFailed =>
			{
				// 死んだ
				Enabled = false;
			});
	}

	public class DmdataEew : IEew
	{
		public DmdataEew(string id, string sourceDisplay, bool isCancelled, DateTime receiveTime)
		{
			Id = id;
			SourceDisplay = sourceDisplay;
			IsCancelled = isCancelled;
			ReceiveTime = receiveTime;
		}

		public string Id { get; }

		public string SourceDisplay { get; }

		public bool IsCancelled { get; }

		public bool IsTrueCancelled => IsCancelled;

		public DateTime ReceiveTime { get; }

		public JmaIntensity Intensity { get; init; } = JmaIntensity.Unknown;

		public DateTime OccurrenceTime { get; init; }

		public string? Place { get; init; }

		public KyoshinMonitorLib.Location? Location { get; init; }

		public float? Magnitude { get; init; }

		public int Depth { get; init; }

		public int Count { get; init; }

		public bool IsWarning { get; init; }

		public bool IsFinal { get; init; }

		public bool IsAccuracyFound { get; init; }

		public int? LocationAccuracy { get; set; }
		public int? DepthAccuracy { get; set; }
		public int? MagnitudeAccuracy { get; set; }

		public bool IsTemporaryEpicenter { get; init; }

		public bool? IsLocked { get; init; }

		public int Priority => 1;

		public DateTime UpdatedTime { get; set; } = TimerService.Default.CurrentTime;
	}
}
