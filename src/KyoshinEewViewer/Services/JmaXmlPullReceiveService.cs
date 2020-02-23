using KyoshinEewViewer.Models;
using KyoshinMonitorLib;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace KyoshinEewViewer.Services
{
	public class JmaXmlPullReceiveService
	{
		public JmaXmlPullReceiveService(ConfigurationService configService, LoggerService logger, IEventAggregator eventAggregator)
		{
			ConfigService = configService;
			Logger = logger ?? throw new ArgumentNullException(nameof(logger));
			EarthquakeUpdatedEvent = eventAggregator.GetEvent<Events.EarthquakeUpdated>();

			TimeElapsed = eventAggregator.GetEvent<Events.TimeElapsed>();

			CacheFolderName = Path.Combine(Path.GetTempPath(), "XmlCache");
		}
		private string CacheFolderName { get; }

		public List<Earthquake> Earthquakes { get; } = new List<Earthquake>();
		private ConfigurationService ConfigService { get; }
		private LoggerService Logger { get; }
		private Events.TimeElapsed TimeElapsed { get; }
		private Events.EarthquakeUpdated EarthquakeUpdatedEvent { get; }
		private HttpClient Client { get; } = new HttpClient();
		private List<Guid> ParsedMessages { get; } = new List<Guid>();

		private DateTime LastElapsedTime { get; set; } = DateTime.MinValue;
		private DateTime LastChecked { get; set; } = DateTime.MinValue;

		private DateTimeOffset? LongFeedLastModified { get; set; }
		private DateTimeOffset? ShortFeedLastModified { get; set; }

		private XmlSerializer ReportSerializer { get; } = new XmlSerializer(typeof(Report));
		private readonly string[] ParseTitles = { "震度速報", "震源に関する情報", "震源・震度に関する情報" };


		public async void Initalize()
		{
			Logger.Info("長期フィード受信中...");
			await ProcessFeed(true, true);
			Logger.Info("短期フィード受信中...");
			await ProcessFeed(false, true);
			EarthquakeUpdatedEvent.Publish(null);

			TimeElapsed.Subscribe(async t =>
			{
				if (LastElapsedTime > t)
					return;
				var prev = LastElapsedTime;
				LastElapsedTime = t;
				if (prev.Second != 19 || t.Second != 20) // 毎時20秒から処理開始
					return;

				// 最後の処理から50秒未満であればそのまま終了
				if (DateTime.Now - LastChecked < TimeSpan.FromSeconds(50))
					return;
				LastChecked = DateTime.Now;

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
			if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
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
				var guid = new Guid(new Uri(new Uri(item.Id).LocalPath).LocalPath);
				if (ParsedMessages.Contains(guid))
					continue;

				Logger.Trace($"処理 {item.LastUpdatedTime:yyyy/MM/dd HH:mm:ss} {item.Title.Text}");

				if (!Directory.Exists(CacheFolderName))
					Directory.CreateDirectory(CacheFolderName);

				int retry = 0;
				// リトライループ
				while (true)
				{
					var cresponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, item.Links[0].Uri));
					if (cresponse.StatusCode != System.Net.HttpStatusCode.OK)
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
						var eq = Earthquakes.FirstOrDefault(e => e.Id == report.Head.EventID);
						if (!useLongFeed || eq == null)
						{
							if (eq == null)
							{
								eq = new Earthquake
								{
									Id = report.Head.EventID,
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
										eq.IsSokuhou = true;
										eq.OccurrenceTime = report.Head.TargetDateTime;
										eq.IsReportTime = true;

										var infoItem = report.Head.Headline.Informations.First().Items.First();
										eq.Intensity = infoItem.Kind.Name.Replace("震度", "").ToJmaIntensity();
										eq.Place = infoItem.Areas.Area.First().Name;
										break;
									}
								// TODO: 震源情報のあとに速報が来ることがある
								case "震源に関する情報":
									{
										eq.IsSokuhou = false;
										eq.OccurrenceTime = report.Body.Earthquake.OriginTime;
										eq.IsReportTime = false;

										eq.Place = report.Body.Earthquake.Hypocenter.Area.Name;
										eq.Magnitude = report.Body.Earthquake.Magnitude.Value;
										eq.Depth = report.Body.Earthquake.Hypocenter.Area.Coordinate.GetDepth();
										eq.IsVeryShallow = eq.Depth <= 0;
										break;
									}
								case "震源・震度に関する情報":
									{
										eq.IsSokuhou = false;
										eq.OccurrenceTime = report.Body.Earthquake.OriginTime;
										eq.IsReportTime = false;

										eq.Intensity = report.Body.Intensity.Observation.MaxInt.ToJmaIntensity();
										eq.Place = report.Body.Earthquake.Hypocenter.Area.Name;
										eq.Magnitude = report.Body.Earthquake.Magnitude.Value;
										eq.Depth = report.Body.Earthquake.Hypocenter.Area.Coordinate.GetDepth();
										eq.IsVeryShallow = eq.Depth <= 0;
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
					ParsedMessages.Add(guid);
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

	#region 簡易デシリアライズ用クラス
#pragma warning disable CA2235 // Mark all non-serializable fields

	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/", IsNullable = true)]
	public class Report
	{
		public ReportControl Control { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
		public Head Head { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
		public Body Body { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/")]
	public class ReportControl
	{
		public string Title { get; set; }
		public DateTime DateTime { get; set; }
		public string Status { get; set; }
		public string EditorialOffice { get; set; }
		public string PublishingOffice { get; set; }
	}

	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/", IsNullable = false)]
	public class Head
	{
		public string Title { get; set; }
		public DateTime ReportDateTime { get; set; }
		public DateTime TargetDateTime { get; set; }
		public ulong EventID { get; set; }
		public string InfoType { get; set; }
		public string InfoKind { get; set; }
		public string InfoKindVersion { get; set; }
		public Headline Headline { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class Headline
	{
		public string Text { get; set; }
		[XmlElement("Information")]
		public HeadlineInformation[] Informations { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformation
	{
		[XmlElement("Item")]
		public HeadlineInformationItem[] Items { get; set; }
		[XmlAttribute("type")]
		public string Type { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItem
	{
		public HeadlineInformationItemKind Kind { get; set; }
		public HeadlineInformationItemAreas Areas { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItemKind
	{
		public string Name { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItemAreas
	{
		[XmlElement("Area")]
		public HeadlineInformationItemAreasArea[] Area { get; set; }
		[XmlAttribute("codeType")]
		public string CodeType { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItemAreasArea
	{
		public string Name { get; set; }
		public uint Code { get; set; }
	}


	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/", IsNullable = true)]
	public class Body
	{
		public BodyEarthquake Earthquake { get; set; }
		public Intensity Intensity { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	public class BodyEarthquake
	{
		public DateTime OriginTime { get; set; }
		public DateTime ArrivalTime { get; set; }
		public EarthquakeHypocenter Hypocenter { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
		public Magnitude Magnitude { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	public class EarthquakeHypocenter
	{
		public BodyEarthquakeHypocenterArea Area { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	public class BodyEarthquakeHypocenterArea
	{
		public string Name { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
		public Coordinate Coordinate { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/", IsNullable = false)]
	public class Coordinate
	{
		[XmlAttribute("description")]
		public string Description { get; set; }
		[XmlAttribute("datum")]
		public string Datum { get; set; }
		[XmlText]
		public string Value { get; set; }

		public int GetDepth()
		{
			var val = Value.Replace("/", "");
			var index = val.LastIndexOf('-');
			if (index < 0)
				return 0;
			if (!int.TryParse(val[index..], out var depth))
				return -1;
			return (-depth) / 1000;
		}
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/", IsNullable = false)]
	public class Magnitude
	{
		[XmlAttribute("type")]
		public string Type { get; set; }
		[XmlAttribute("description")]
		public string Description { get; set; }
		[XmlText]
		public float Value { get; set; }
	}

	[Serializable()]
	[XmlType(AnonymousType = true)]
	public class Intensity
	{
		public IntensityObservation Observation { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true)]
	public class IntensityObservation
	{
		public string MaxInt { get; set; }
	}
#pragma warning restore CA2235 // Mark all non-serializable fields
	#endregion
}
