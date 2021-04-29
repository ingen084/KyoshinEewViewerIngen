using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.InformationProvider
{
	public class JmaXmlPullProvider
	{
		public event Action NewFeedArrived;

		public JmaXmlPullProvider()
		{
			MessageBus.Current.Listen<TimerElapsed>().Subscribe(async t =>
			{
				if (LastElapsedTime > t.Time || !Enabled)
					return;
				var prev = LastElapsedTime;
				LastElapsedTime = t.Time;
				if (prev.Second != 19 || t.Time.Second != 20) // 毎時20秒から処理開始
					return;

				// 最後の処理から50秒未満であればそのまま終了
				if (DateTime.UtcNow - LastChecked < TimeSpan.FromSeconds(50))
					return;
				LastChecked = DateTime.UtcNow;

				Logger.Info("短期フィード受信中...");
				try
				{
					await ProcessFeed(false, false);
				}
				catch (Exception ex)
				{
					Logger.Warning("短期フィードの受信中に例外が発生しました。\n" + ex);
				}
			});
		}
		private string CacheFolderName { get; }

		public List<Earthquake> Earthquakes { get; } = new List<Earthquake>();
		private LoggerService Logger { get; }
		private HttpClient Client { get; } = new HttpClient(new HttpClientHandler()
		{
			AutomaticDecompression = DecompressionMethods.All
		});
		private List<string> ParsedMessages { get; } = new List<string>();

		private DateTime LastElapsedTime { get; set; } = DateTime.MinValue;
		private DateTime LastChecked { get; set; } = DateTime.MinValue;

		private DateTimeOffset? LongFeedLastModified { get; set; }
		private DateTimeOffset? ShortFeedLastModified { get; set; }

		private XmlSerializer ReportSerializer { get; } = new XmlSerializer(typeof(Report));
		private readonly string[] ParseTitles = { "震度速報", "震源に関する情報", "震源・震度に関する情報" };

		public bool Enabled { get; private set; } = false;

		public async Task EnableAsync()
		{
			if (Enabled)
				return;
			Logger.Info("JMAXMLを有効化しています。");
			// 短期フィードを過去に受信したことがないか、追跡時間を過ぎている場合長期フィードを受信する
			if (ShortFeedLastModified is not DateTimeOffset mod || (DateTimeOffset.Now - mod).TotalMinutes > 9)
			{
				Logger.Info("長期フィード受信中...");
				await ProcessFeed(true, true);
			}
			Logger.Info("短期フィード受信中...");
			await ProcessFeed(false, true);
			EarthquakeUpdatedEvent.Publish(null);
			Enabled = true;
		}
		public void Disable()
		{
			if (!Enabled)
				return;
			Logger.Info("JMAXMLを無効化しています。");
			Enabled = false;
		}

		private async Task ProcessFeed(bool useLongFeed, bool disableNotice)
		{
			using var request = new HttpRequestMessage(HttpMethod.Get,
				useLongFeed
				? "http://www.data.jma.go.jp/developer/xml/feed/eqvol_l.xml"
				: "http://www.data.jma.go.jp/developer/xml/feed/eqvol.xml");

			DateTimeOffset? lastModified;
			if (useLongFeed)
				lastModified = LongFeedLastModified;
			else
				lastModified = ShortFeedLastModified;

			// 初回取得じゃない場合チェックしてもらう
			if (lastModified != null)
				request.Headers.IfModifiedSince = lastModified;
			using var response = await Client.SendAsync(request);
			if (response.StatusCode == HttpStatusCode.NotModified)
			{
				Logger.Debug("JMAXMLフィード - NotModified");
				return;
			}
			Logger.Debug($"JMAXMLフィード更新処理開始 Last:{lastModified:yyyy/MM/dd HH:mm:ss} Current:{response.Content.Headers.LastModified:yyyy/MM/dd HH:mm:ss}");

			using var reader = XmlReader.Create(await response.Content.ReadAsStreamAsync());
			var feed = SyndicationFeed.Load(reader);

			// 求めているものを新しい順で舐める
			var matchItems = feed.Items.Where(i => ParseTitles.Any(t => t == i.Title.Text));
			if (useLongFeed)
				matchItems = matchItems.OrderByDescending(i => i.LastUpdatedTime);
			else
				matchItems = matchItems.OrderBy(i => i.LastUpdatedTime);

			// URLにないものを抽出
			foreach (var item in matchItems)
			{
				if (ParsedMessages.Contains(item.Id))
					continue;

				Logger.Trace($"処理 {item.LastUpdatedTime:yyyy/MM/dd HH:mm:ss} {item.Title.Text}");

				if (!Directory.Exists(CacheFolderName))
					Directory.CreateDirectory(CacheFolderName);

				int retry = 0;
				// リトライループ
				while (true)
				{
					var cresponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, item.Links[0].Uri));
					if (cresponse.StatusCode != HttpStatusCode.OK)
					{
						retry++;
						if (retry >= 10)
						{
							Logger.Warning($"XMLの取得に失敗しました！ Status: {cresponse.StatusCode} Url: {item.Links[0].Uri}");
							break;
						}
						continue;
					}
					// TODO: もう少し綺麗にしたい
					try
					{
						var cacheFilePath = Path.Join(CacheFolderName, Path.GetFileName(item.Links[0].Uri.ToString()));
						if (!File.Exists(cacheFilePath))
						{
							using var wstr = File.OpenWrite(cacheFilePath);
							await (await cresponse.Content.ReadAsStreamAsync()).CopyToAsync(wstr);
						}
						using var rstr = File.OpenRead(cacheFilePath);
						var report = (Report)ReportSerializer.Deserialize(rstr);

						// TODO: EventIdの異なる電文に対応する
						var eq = Earthquakes.FirstOrDefault(e => e.Id == report.Head.EventID);
						if (!useLongFeed || eq == null)
						{
							if (eq == null)
							{
								eq = new Earthquake
								{
									Id = report.Head.EventID,
									IsSokuhou = true,
									IsHypocenterOnly = false,
									Intensity = JmaIntensity.Unknown
								};
								if (useLongFeed)
									Earthquakes.Add(eq);
								else
									Earthquakes.Insert(0, eq);
							}

							switch (report.Control.Title)
							{
								case "震度速報":
									{
										// 
										var infoItem = report.Head.Headline.Informations.First().Items.First();
										eq.Intensity = infoItem.Kind.Name.Replace("震度", "").ToJmaIntensity();

										// すでに他の情報が入ってきている場合更新を行わない
										if (!eq.IsSokuhou)
											break;
										// eq.IsHypocenterOnly = false;
										eq.IsSokuhou = true;
										eq.OccurrenceTime = report.Head.TargetDateTime;
										eq.IsReportTime = true;

										eq.Place = infoItem.Areas.Area.First().Name;
										break;
									}
								case "震源に関する情報":
									{
										if (eq.IsSokuhou)
										{
											eq.IsHypocenterOnly = true;
											eq.IsSokuhou = false;
										}
										eq.OccurrenceTime = report.Body.Earthquake.OriginTime;
										eq.IsReportTime = false;

										eq.Place = report.Body.Earthquake.Hypocenter.Area.Name;
										eq.Magnitude = report.Body.Earthquake.Magnitude.Value;
										eq.Depth = report.Body.Earthquake.Hypocenter.Area.Coordinate.GetDepth() ?? -1;
										break;
									}
								case "震源・震度に関する情報":
									{
										eq.IsSokuhou = false;
										eq.IsHypocenterOnly = false;
										eq.OccurrenceTime = report.Body.Earthquake.OriginTime;
										eq.IsReportTime = false;

										eq.Intensity = report.Body.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;
										eq.Place = report.Body.Earthquake.Hypocenter.Area.Name;
										eq.Magnitude = report.Body.Earthquake.Magnitude.Value;
										eq.Depth = report.Body.Earthquake.Hypocenter.Area.Coordinate.GetDepth() ?? -1;
										break;
									}
								default:
									Logger.Error("不明なTitleをパースしました。: " + report.Control.Title);
									break;
							}
							if (!disableNotice)
								EarthquakeUpdatedEvent.Publish(eq);
						}
					}
					catch (Exception ex)
					{
						Logger.Error("デシリアライズ時に例外が発生しました。 " + ex);
					}
					ParsedMessages.Add(item.Id);
					break;
				}

				if (useLongFeed && Earthquakes.Count > 10)
					break;
			}
			if (!useLongFeed && Earthquakes.Count > 20)
				Earthquakes.RemoveRange(20, Earthquakes.Count - 20);
			if (ParsedMessages.Count > 100)
				ParsedMessages.RemoveRange(100, ParsedMessages.Count - 100);

			if (useLongFeed)
				LongFeedLastModified = response.Content.Headers?.LastModified?.UtcDateTime;
			else
				ShortFeedLastModified = response.Content.Headers?.LastModified?.UtcDateTime;

			// キャッシュフォルダのクリーンアップ
			var cachedFiles = Directory.GetFiles(CacheFolderName, "*.xml");
			if (cachedFiles.Length < 20) // 20件以内なら削除しない
				return;
			foreach (var file in cachedFiles)
				if (DateTime.Now - File.GetCreationTime(file) > TimeSpan.FromDays(10))
					File.Delete(file);
		}
	}
}
