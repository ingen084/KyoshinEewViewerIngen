using Avalonia.Controls;
using DmdataSharp.ApiResponses.V2.Parameters;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Sentry;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using U8Xml;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 地震情報の更新を担う
/// </summary>
public class EarthquakeWatchService : ReactiveObject
{
	private readonly string[] TargetTitles = { "震度速報", "震源に関する情報", "震源・震度に関する情報", "顕著な地震の震源要素更新のお知らせ" };

	private NotificationService? NotificationService { get; }
	public EarthquakeStationParameterResponse? Stations { get; private set; }
	public ObservableCollection<Models.Earthquake> Earthquakes { get; } = new();
	public event Action<Models.Earthquake, bool>? EarthquakeUpdated;

	public event Action? Failed;
	public event Action? SourceSwitching;
	public event Action<string>? SourceSwitched;

	private SoundCategory SoundCategory { get; } = new("Earthquake", "地震情報");
	private Sound UpdatedSound { get; }
	private Sound IntensityUpdatedSound { get; }
	private Sound UpdatedTrainingSound { get; }

	private ILogger Logger { get; }

	public EarthquakeWatchService(NotificationService? notificationService, TelegramProvideService telegramProvider)
	{
		Logger = LoggingService.CreateLogger(this);
		NotificationService = notificationService;

		UpdatedSound = SoundPlayerService.RegisterSound(SoundCategory, "Updated", "地震情報の更新", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "4" }, });
		IntensityUpdatedSound = SoundPlayerService.RegisterSound(SoundCategory, "IntensityUpdated", "震度の更新", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "4" }, });
		UpdatedTrainingSound = SoundPlayerService.RegisterSound(SoundCategory, "TrainingUpdated", "地震情報の更新(訓練)", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "6+" }, });

		if (Design.IsDesignMode)
			return;

		telegramProvider.Subscribe(
			InformationCategory.Earthquake,
			async (s, t) =>
			{
				SourceSwitching?.Invoke();

				if (s.Contains("DM-D.S.S") && Stations == null && DmdataTelegramPublisher.Instance != null)
					try
					{
						Stations = await DmdataTelegramPublisher.Instance.GetEarthquakeStationsAsync();
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "観測点情報取得中に問題が発生しました");
					}

				Earthquakes.Clear();
				// クリア直後に操作してしまうとUI要素構築とバッティングしてしまうためちょっと待機する
				foreach (var h in t.OrderBy(h => h.ArrivalTime))
				{
					try
					{
						ProcessInformation(h.Key, await h.GetBodyAsync(), hideNotice: true);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "キャッシュ破損疑いのため削除します");
						try
						{
							// キャッシュ破損時用
							h.Cleanup();
							ProcessInformation(h.Key, await h.GetBodyAsync(), hideNotice: true);
						}
						catch (Exception ex2)
						{
							// その他のエラー発生時は処理を中断させる
							Logger.LogError(ex2, "初回電文取得中に問題が発生しました");
						}
						return;
					}
				}
				// 電文データがない(震源情報しかないなどの)データを削除する
				foreach (var eq in Earthquakes.Where(e => !e.UsedModels.Any()).ToArray())
					Earthquakes.Remove(eq);

				foreach (var eq in Earthquakes)
					EarthquakeUpdated?.Invoke(eq, true);
				SourceSwitched?.Invoke(s);
			},
			async t =>
			{
				var trans = SentrySdk.StartTransaction("earthquake", "arrived");
				try
				{
					var stream = await t.GetBodyAsync();
					ProcessInformation(t.Key, stream);
					trans.Finish();
				}
				catch (Exception ex)
				{
					trans.Finish(ex);
				}
			},
			s =>
			{
				if (s.isAllFailed)
					Failed?.Invoke();
				else
					SourceSwitching?.Invoke();
			});
	}

	// MEMO: 内部で stream は dispose します
	public Models.Earthquake? ProcessInformation(string id, Stream stream, bool dryRun = false, bool hideNotice = false)
	{
		using (stream)
		{
			using var report = new JmaXmlDocument(stream);

			try
			{
				// サポート外であれば見なかったことにする
				if (!TargetTitles.Contains(report.Control.Title))
					return null;

				// 保存されている Earthquake インスタンスを抜き出してくる
				var eq = Earthquakes.FirstOrDefault(e => e?.Id == report.Head.EventId);
				if (eq == null || dryRun)
				{
					eq = new Models.Earthquake(report.Head.EventId)
					{
						IsSokuhou = true,
						IsHypocenterOnly = false,
						Intensity = JmaIntensity.Unknown
					};
					if (!dryRun)
						Earthquakes.Insert(0, eq);
				}

				// すでに処理済みであったばあいそのまま帰る
				if (eq.UsedModels.Any(m => m.Id == id))
					return eq;

				// 情報更新前の震度
				var prevInt = eq.Intensity;

				// 訓練報チェック 1回でも訓練報を読んだ記録があれば訓練扱いとする
				if (!eq.IsTraining)
					eq.IsTraining = report.Control.Status != "通常";

				// Head
				eq.HeadlineText = report.Head.Headline.Text;
				eq.HeadTitle = report.Head.Title;

				// 震度速報をパースする
				void ProcessVxse51()
				{
					// すでに他の情報が入ってきている場合更新を行わない
					if (!eq.IsSokuhou)
						return;
					string? areaName = null;
					var isOnlyPosition = true;

					if (report.EarthquakeBody.Intensity?.Observation is not IntensityObservation observation)
						throw new EarthquakeWatchException("Observation がみつかりません");

					eq.IsSokuhou = true;
					eq.Intensity = observation.MaxInt?.ToJmaIntensity() ?? throw new EarthquakeWatchException("MaxInt がみつかりません");

					foreach (var pref in observation.Prefs)
					{
						// すでに複数件存在することが判明していれば戻る
						if (!isOnlyPosition)
							break;
						foreach (var area in pref.Areas)
						{
							// すでに area の取得ができていれば複数箇所存在するフラグを立てる
							if (areaName != null && isOnlyPosition)
							{
								isOnlyPosition = false;
								break;
							}
							// 未取得であれば area に代入
							areaName = area.Name;
						}
					}

					// すでに震源情報を受信していない場合のみ更新
					if (!eq.IsHypocenterOnly)
					{
						eq.OccurrenceTime = report.Head.TargetDateTime?.DateTime ?? report.Control.DateTime.DateTime;
						eq.IsTargetTime = true;

						if (areaName == null)
							throw new EarthquakeWatchException("Area.Name がみつかりません");
						eq.Place = areaName;
						eq.IsOnlypoint = isOnlyPosition;
					}
				}

				// 震源情報をパースする
				void ProcessHypocenter()
				{
					if (report.EarthquakeBody.Earthquake is not EarthquakeData earthquake)
						throw new EarthquakeWatchException("Earthquake がみつかりません");

					eq.OccurrenceTime = earthquake.OriginTime?.DateTime ?? throw new EarthquakeWatchException("OriginTime がみつかりません");
					eq.IsTargetTime = false;

					// すでに他の情報が入ってきている場合更新だけ行う
					if (eq.IsSokuhou)
						eq.IsHypocenterOnly = true;

					eq.Place = earthquake.Hypocenter.Area.Name;
					eq.IsOnlypoint = true;

					eq.Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m : throw new EarthquakeWatchException("magnitude がfloatにパースできません");
					string? magnitudeDescription = null;
					if (float.IsNaN(eq.Magnitude) && earthquake.Magnitude.Description is string desc)
						magnitudeDescription = desc;
					eq.MagnitudeAlternativeText = magnitudeDescription;

					var depth = -1;
					foreach (var c in earthquake.Hypocenter.Area.Coordinates)
					{
						// 度分 のときは深さだけ更新する
						if (c.Type == "震源位置（度分）")
						{
							depth = CoordinateConverter.GetDepth(c.Value) ?? depth;
							continue;
						}
						eq.Location = CoordinateConverter.GetLocation(c.Value);
						depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
					}
					eq.Depth = depth;

					// コメント部分
					if (report.EarthquakeBody.Comments?.ForecastCommentText is string forecastCommentText)
						eq.Comment = forecastCommentText;
					if (report.EarthquakeBody.Comments?.FreeFormComment is string freeformCommentText)
						eq.FreeFormComment = freeformCommentText;
				}

				// 震源震度情報をパースする
				void ProcessVxse53()
				{
					// 震源情報を処理
					ProcessHypocenter();

					eq.IsSokuhou = false;
					eq.IsHypocenterOnly = false;

					// 最大震度
					eq.Intensity = report.EarthquakeBody.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

					// コメント部分
					if (report.EarthquakeBody.Comments?.ForecastCommentText is string forecastCommentText)
						eq.Comment = forecastCommentText;
					if (report.EarthquakeBody.Comments?.FreeFormComment is string freeformCommentText)
						eq.FreeFormComment = freeformCommentText;
				}

				// 種類に応じて解析
				var isSkipAddUsedModel = false;
				switch (report.Control.Title)
				{
					case "震源に関する情報":
					case "顕著な地震の震源要素更新のお知らせ":
						ProcessHypocenter();
						isSkipAddUsedModel = true;
						break;
					case "震度速報":
						ProcessVxse51();
						break;
					case "震源・震度に関する情報":
						ProcessVxse53();
						break;
					default:
						Logger.LogError("不明なTitleをパースしました。: {title}", report.Control.Title);
						break;
				}
				if (!isSkipAddUsedModel)
					eq.UsedModels.Add(new Models.ProcessedTelegram(id, report.Control.DateTime.DateTime, report.Control.Title));

				if (!hideNotice)
				{
					EarthquakeUpdated?.Invoke(eq, false);
					if (!dryRun)
					{
						var intStr = eq.Intensity.ToShortString().Replace('*', '-');
						if (
							(!eq.IsTraining || !UpdatedTrainingSound.Play(new() { { "int", intStr } })) &&
							(eq.Intensity == prevInt || !IntensityUpdatedSound.Play(new() { { "int", intStr } }))
						)
							UpdatedSound.Play(new() { { "int", intStr } });
						if (ConfigurationService.Current.Notification.GotEq)
							NotificationService?.Notify($"{eq.Title}", eq.GetNotificationMessage());
					}
				}
				return eq;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "デシリアライズ時に例外が発生しました");
				return null;
			}
		}
	}
}
