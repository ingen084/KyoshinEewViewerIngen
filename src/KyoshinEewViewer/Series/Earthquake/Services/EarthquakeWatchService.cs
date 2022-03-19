using Avalonia.Controls;
using DmdataSharp.ApiResponses.V2.Parameters;
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

	private NotificationService NotificationService { get; }
	public EarthquakeStationParameterResponse? Stations { get; private set; }
	public ObservableCollection<Models.Earthquake> Earthquakes { get; } = new();
	public event Action<Models.Earthquake, bool>? EarthquakeUpdated;

	public event Action? Failed;
	public event Action? SourceSwitching;
	public event Action<string>? SourceSwitched;

	private SoundPlayerService.SoundCategory SoundCategory { get; } = new("Earthquake", "地震情報");
	private SoundPlayerService.Sound UpdatedSound { get; }
	private SoundPlayerService.Sound IntensityUpdatedSound { get; }
	private SoundPlayerService.Sound UpdatedTrainingSound { get; }

	private ILogger Logger { get; }

	public EarthquakeWatchService(NotificationService notificationService, TelegramProvideService telegramProvider)
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

				if (Stations == null && DmdataTelegramPublisher.Instance != null)
					try
					{
						Stations = await DmdataTelegramPublisher.Instance.GetEarthquakeStationsAsync();
					}
					catch (Exception ex)
					{
						Logger.LogError("観測点情報取得中に問題が発生しました: {ex}", ex);
					}

				Earthquakes.Clear();
				// クリア直後に操作してしまうとUI要素構築とバッティングしてしまうためちょっと待機する
				await Task.Delay(100);
				foreach (var h in t.OrderBy(h => h.ArrivalTime))
				{
					try
					{
						ProcessInformation(h.Key, await h.GetBodyAsync(), hideNotice: true);
					}
					catch (Exception ex)
					{
						Logger.LogError("キャッシュ破損疑いのため削除します: {ex}", ex);
						try
						{
							// キャッシュ破損時用
							h.Cleanup();
							ProcessInformation(h.Key, await h.GetBodyAsync(), hideNotice: true);
						}
						catch (Exception ex2)
						{
							// その他のエラー発生時は処理を中断させる
							Logger.LogError("初回電文取得中に問題が発生しました: {ex}", ex2);
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
			isAllFailed =>
			{
				if (isAllFailed)
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
			using var reader = XmlParser.Parse(stream);

			string? title = null;
			DateTime dateTime = default;
			DateTime targetDateTime = default;
			string? eventId = null;
			string? status = null;

			try
			{
				// 原則として foreach 内の変数は親のノード名で行う
				// Report / Control 用ループ
				foreach (var report in reader.Root.Children)
				{
					switch (report.Name.ToString())
					{
						// /Report/Control
						case "Control":
							foreach (var control in report.Children)
							{
								switch (control.Name.ToString())
								{
									// /Report/Control/Title
									case "Title":
										title = control.InnerText.ToString();
										// サポート外の
										if (!TargetTitles.Contains(title))
											return null;
										break;

									// /Report/Control/DateTime
									case "DateTime":
										if (!DateTime.TryParse(control.InnerText.ToString(), out dateTime))
											throw new EarthquakeWatchException("DateTimeをパースできませんでした");
										break;

									// /Report/Control/Status
									case "Status":
										status = control.InnerText.ToString();
										break;
								}
							}
							break;

						// /Report/Head
						case "Head":
							foreach (var head in report.Children)
							{
								// /Report/Head/Title
								switch (head.Name.ToString())
								{
									case "EventID":
										eventId = head.InnerText.ToString();
										break;
									case "TargetDateTime":
										if (!DateTime.TryParse(head.InnerText.ToString(), out targetDateTime))
											throw new EarthquakeWatchException("TargetDateTimeをパースできませんでした");
										break;
								}
							}
							break;
					}
				}

				// null チェック
				if (title is null)
					throw new EarthquakeWatchException("Titleがみつかりません");
				if (dateTime == default)
					throw new EarthquakeWatchException("DateTimeがみつかりません");
				if (status is null)
					throw new EarthquakeWatchException("Statusがみつかりません");
				if (eventId is null)
					throw new EarthquakeWatchException("EventIDを解析できませんでした");

				// 保存されている Earthquake インスタンスを抜き出してくる
				var eq = Earthquakes.FirstOrDefault(e => e?.Id == eventId);
				if (eq == null || dryRun)
				{
					eq = new Models.Earthquake(eventId)
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

				var prevInt = eq.Intensity;

				// /Report/Body をとってくる
				if (!reader.Root.TryFindChild("Body", out var bodyElement))
					throw new EarthquakeWatchException("Body がみつかりません");

				// 訓練報チェック 1回でも訓練報を読んだ記録があれば訓練扱いとする
				if (!eq.IsTraining)
					eq.IsTraining = status is "訓練";

				// 震度速報をパースする
				void ProcessVxse51(XmlNode bodyNode, Models.Earthquake eq)
				{
					// すでに他の情報が入ってきている場合更新を行わない
					if (!eq.IsSokuhou)
						return;
					string? area = null;
					var isOnlyPosition = true;
					JmaIntensity intensity = default;

					if (!bodyNode.TryFindChild("Intensity", out var intensityNode))
						throw new EarthquakeWatchException("Intensity がみつかりません");
					if (!intensityNode.TryFindChild("Observation", out var observationNode))
						throw new EarthquakeWatchException("Observation がみつかりません");

					// /Report/Body/Intensity/Observation
					foreach (var observation in observationNode.Children)
					{
						switch (observation.Name.ToString())
						{
							// /Report/Body/Intensity/Observation/MaxInt
							case "MaxInt":
								intensity = observation.InnerText.ToString().ToJmaIntensity();
								break;
							// /Report/Body/Intensity/Observation/Pref
							case "Pref":
								// すでに複数件存在することが判明していれば戻る
								if (!isOnlyPosition)
									break;
								foreach (var pref in observation.Children)
								{
									if (pref.Name.ToString() != "Area")
										continue;
									// /Report/Body/Intensity/Observation/Pref/Area
									if (!pref.TryFindChild("Name", out var rawName))
										throw new EarthquakeWatchException("Area.Name がみつかりません");

									// すでに area の取得ができていれば複数箇所存在するフラグを立てる
									if (area is not null && isOnlyPosition)
									{
										isOnlyPosition = false;
										break;
									}
									// 未取得であれば area に代入
									area = rawName.InnerText.ToString();
								}
								break;
						}
					}

					if (intensity == default)
						throw new EarthquakeWatchException("MaxInt がみつかりません");

					eq.IsSokuhou = true;
					eq.Intensity = intensity;

					// すでに震源情報を受信していない場合のみ更新
					if (!eq.IsHypocenterOnly)
					{
						if (targetDateTime == default)
							throw new EarthquakeWatchException("DateTimeがみつかりません");

						eq.OccurrenceTime = targetDateTime;
						eq.IsReportTime = true;

						if (area is null)
							throw new EarthquakeWatchException("Area.Name がみつかりません");
						eq.Place = area;
						eq.IsOnlypoint = isOnlyPosition;
					}
				}

				// 震源情報をパースする
				void ProcessVxse52(XmlNode bodyNode, Models.Earthquake eq)
				{
					if (!bodyNode.TryFindChild("Earthquake", out var earthquakeNode))
						throw new EarthquakeWatchException("Earthquake がみつかりません");

					DateTime originTime = default;
					string? place = null;
					KyoshinMonitorLib.Location? location = null;
					var depth = -1;
					var magnitude = float.NaN;
					string? magnitudeDescription = null;

					// /Report/Body/Earthquake
					foreach (var earthquake in earthquakeNode.Children)
					{
						switch (earthquake.Name.ToString())
						{
							// /Report/Body/Earthquake/OriginTime
							case "OriginTime":
								if (!DateTime.TryParse(earthquake.InnerText.ToString(), out originTime))
									throw new EarthquakeWatchException("OriginTime をパースできませんでした");
								break;
							// /Report/Body/Earthquake/Hypocenter
							case "Hypocenter":
								if (!earthquake.TryFindChild("Area", out var areaNode))
									throw new EarthquakeWatchException("Hypocenter.Area がみつかりません");
								foreach (var area in areaNode.Children)
								{
									switch (area.Name.ToString())
									{
										// /Report/Body/Earthquake/Hypocenter/Name
										case "Name":
											place = area.InnerText.ToString();
											break;
										// /Report/Body/Earthquake/Hypocenter/jmx_eb:Coordinate
										case "jmx_eb:Coordinate":
											var innerText = area.InnerText.ToString();
											// 度分 のときは深さだけ更新する
											// /Report/Body/Earthquake/Hypocenter/jmx_eb:Coordinate@type
											if (area.TryFindAttribute("type", out var typeAttr) && typeAttr.Value.ToString() == "震源位置（度分）")
											{
												depth = CoordinateConverter.GetDepth(innerText) ?? depth;
												break;
											}
											location = CoordinateConverter.GetLocation(innerText);
											depth = CoordinateConverter.GetDepth(innerText) ?? -1;
											break;
									}
								}
								break;
							// /Report/Body/Earthquake/jmx_eb:Magnitude
							case "jmx_eb:Magnitude":
								magnitude = earthquake.InnerText.ToFloat32();
								if (float.IsNaN(magnitude) && earthquake.TryFindAttribute("description", out var descAttr))
									magnitudeDescription = descAttr.Value.ToString();
								break;
						}
					}

					if (originTime == default)
						throw new EarthquakeWatchException("OriginTime がみつかりません");
					eq.OccurrenceTime = originTime;
					eq.IsReportTime = false;

					// すでに他の情報が入ってきている場合更新だけ行う
					if (eq.IsSokuhou)
						eq.IsHypocenterOnly = true;

					eq.Place = place ?? throw new EarthquakeWatchException("Hypocenter.Name がみつかりません");
					eq.IsOnlypoint = true;
					eq.Magnitude = magnitude;
					eq.MagnitudeAlternativeText = magnitudeDescription;
					eq.Location = location;
					eq.Depth = depth;

					// コメント部分
					if (bodyNode.TryFindChild("Comments", out var commentsNode))
					{
						if (commentsNode.TryFindChild("ForecastComment", out var forecastCommentNode) &&
							forecastCommentNode.TryFindChild("Text", out var forecastCommentTextNode))
							eq.Comment = forecastCommentTextNode.InnerText.ToString();
						if (commentsNode.TryFindChild("FreeFormComment", out var freeFormCommentNode))
							eq.FreeFormComment = freeFormCommentNode.InnerText.ToString();
					}
				}

				// 震源震度情報をパースする
				void ProcessVxse53(XmlNode bodyNode, Models.Earthquake eq)
				{
					if (!bodyNode.TryFindChild("Earthquake", out var earthquakeNode))
						throw new EarthquakeWatchException("Earthquake がみつかりません");

					DateTime originTime = default;
					string? place = null;
					KyoshinMonitorLib.Location? location = null;
					var depth = -1;
					var magnitude = float.NaN;
					string? magnitudeDescription = null;

					// /Report/Body/Earthquake
					foreach (var earthquake in earthquakeNode.Children)
					{
						switch (earthquake.Name.ToString())
						{
							// /Report/Body/Earthquake/OriginTime
							case "OriginTime":
								if (!DateTime.TryParse(earthquake.InnerText.ToString(), out originTime))
									throw new EarthquakeWatchException("OriginTime をパースできませんでした");
								break;
							// /Report/Body/Earthquake/Hypocenter
							case "Hypocenter":
								if (!earthquake.TryFindChild("Area", out var areaNode))
									throw new EarthquakeWatchException("Hypocenter.Area がみつかりません");
								foreach (var area in areaNode.Children)
								{
									switch (area.Name.ToString())
									{
										// /Report/Body/Earthquake/Hypocenter/Name
										case "Name":
											place = area.InnerText.ToString();
											break;
										// /Report/Body/Earthquake/Hypocenter/jmx_eb:Coordinate
										case "jmx_eb:Coordinate":
											var innerText = area.InnerText.ToString();
											// 度分 のときは深さだけ更新する
											// /Report/Body/Earthquake/Hypocenter/jmx_eb:Coordinate@type
											if (area.TryFindAttribute("type", out var typeAttr) && typeAttr.Value.ToString() == "震源位置（度分）")
											{
												depth = CoordinateConverter.GetDepth(innerText) ?? depth;
												break;
											}
											location = CoordinateConverter.GetLocation(innerText);
											depth = CoordinateConverter.GetDepth(innerText) ?? -1;
											break;
									}
								}
								break;
							// /Report/Body/Earthquake/jmx_eb:Magnitude
							case "jmx_eb:Magnitude":
								magnitude = earthquake.InnerText.ToFloat32();
								if (float.IsNaN(magnitude) && earthquake.TryFindAttribute("description", out var descAttr))
									magnitudeDescription = descAttr.Value.ToString();
								break;
						}
					}

					if (originTime == default)
						throw new EarthquakeWatchException("OriginTime がみつかりません");
					eq.OccurrenceTime = originTime;
					eq.IsReportTime = false;

					eq.IsSokuhou = false;
					eq.IsHypocenterOnly = false;

					if (bodyNode.TryFindChild("Intensity", out var intensityNode))
					{
						if (!intensityNode.TryFindChild("Observation", out var observationNode))
							throw new EarthquakeWatchException("Observation がみつかりません");
						if (!observationNode.TryFindChild("MaxInt", out var maxIntNode))
							throw new EarthquakeWatchException("MaxInt がみつかりません");
						eq.Intensity = maxIntNode.InnerText.ToString().ToJmaIntensity();
					}
					else
						eq.Intensity = JmaIntensity.Unknown;

					eq.Place = place ?? throw new Exception("Hypocenter.Name がみつかりません");
					eq.IsOnlypoint = true;
					eq.Magnitude = magnitude;
					eq.MagnitudeAlternativeText = magnitudeDescription;
					eq.Location = location;
					eq.Depth = depth;

					// コメント部分
					if (bodyNode.TryFindChild("Comments", out var commentsNode))
					{
						if (commentsNode.TryFindChild("ForecastComment", out var forecastCommentNode) &&
							forecastCommentNode.TryFindChild("Text", out var forecastCommentTextNode))
							eq.Comment = forecastCommentTextNode.InnerText.ToString();
						if (commentsNode.TryFindChild("FreeFormComment", out var freeFormCommentNode))
							eq.FreeFormComment = freeFormCommentNode.InnerText.ToString();
					}
				}

				// 顕著な地震の震源要素更新のお知らせをパースする
				void ProcessVxse61(XmlNode bodyNode, Models.Earthquake eq)
				{
					if (!bodyNode.TryFindChild("Earthquake", out var earthquakeNode))
						throw new EarthquakeWatchException("Earthquake がみつかりません");

					DateTime originTime = default;
					string? place = null;
					KyoshinMonitorLib.Location? location = null;
					var depth = -1;
					var magnitude = float.NaN;
					string? magnitudeDescription = null;

					// /Report/Body/Earthquake
					foreach (var earthquake in earthquakeNode.Children)
					{
						switch (earthquake.Name.ToString())
						{
							// /Report/Body/Earthquake/OriginTime
							case "OriginTime":
								if (!DateTime.TryParse(earthquake.InnerText.ToString(), out originTime))
									throw new EarthquakeWatchException("OriginTime をパースできませんでした");
								break;
							// /Report/Body/Earthquake/Hypocenter
							case "Hypocenter":
								if (!earthquake.TryFindChild("Area", out var areaNode))
									throw new EarthquakeWatchException("Hypocenter.Area がみつかりません");
								foreach (var area in areaNode.Children)
								{
									switch (area.Name.ToString())
									{
										// /Report/Body/Earthquake/Hypocenter/Name
										case "Name":
											place = area.InnerText.ToString();
											break;
										// /Report/Body/Earthquake/Hypocenter/jmx_eb:Coordinate
										case "jmx_eb:Coordinate":
											var innerText = area.InnerText.ToString();
											// 度分 のときは深さだけ更新する
											// /Report/Body/Earthquake/Hypocenter/jmx_eb:Coordinate@type
											if (area.TryFindAttribute("type", out var typeAttr) && typeAttr.Value.ToString() == "震源位置（度分）")
											{
												depth = CoordinateConverter.GetDepth(innerText) ?? depth;
												break;
											}
											location = CoordinateConverter.GetLocation(innerText);
											depth = CoordinateConverter.GetDepth(innerText) ?? -1;
											break;
									}
								}
								break;
							// /Report/Body/Earthquake/jmx_eb:Magnitude
							case "jmx_eb:Magnitude":
								magnitude = earthquake.InnerText.ToFloat32();
								if (float.IsNaN(magnitude) && earthquake.TryFindAttribute("description", out var descAttr))
									magnitudeDescription = descAttr.Value.ToString();
								break;
						}
					}

					if (originTime == default)
						throw new EarthquakeWatchException("OriginTime がみつかりません");
					eq.OccurrenceTime = originTime;
					eq.IsReportTime = false;

					eq.Place = place ?? throw new EarthquakeWatchException("Hypocenter.Name がみつかりません");
					eq.IsOnlypoint = true;
					eq.Magnitude = magnitude;
					eq.MagnitudeAlternativeText = magnitudeDescription;
					eq.Location = location;
					eq.Depth = depth;
				}

				// 種類に応じて解析
				var isSkipAddUsedModel = false;
				switch (title)
				{
					case "震度速報":
						ProcessVxse51(bodyElement, eq);
						break;
					case "震源に関する情報":
						ProcessVxse52(bodyElement, eq);
						isSkipAddUsedModel = true;
						break;
					case "震源・震度に関する情報":
						ProcessVxse53(bodyElement, eq);
						break;
					case "顕著な地震の震源要素更新のお知らせ":
						ProcessVxse61(bodyElement, eq);
						isSkipAddUsedModel = true;
						break;
					default:
						Logger.LogError("不明なTitleをパースしました。: {title}", title);
						break;
				}
				if (!isSkipAddUsedModel)
					eq.UsedModels.Add(new Models.ProcessedTelegram(id, dateTime, title));

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
							NotificationService.Notify($"{eq.Title}", eq.GetNotificationMessage());
					}
				}
				return eq;
			}
			catch (Exception ex)
			{
				Logger.LogError("デシリアライズ時に例外が発生しました。 {ex}", ex);
				return null;
			}
		}
	}
}
