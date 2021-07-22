using KyoshinEewViewer.Core.Models.Events;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;

namespace KyoshinEewViewer.Services.InformationProviders
{
	public class JmaXmlPullProvider : InformationProvider
	{
		private static JmaXmlPullProvider? _default;
		public static JmaXmlPullProvider Default => _default ??= new();

		private ILogger Logger { get; }

		public JmaXmlPullProvider()
		{
			Logger = LoggingService.CreateLogger<JmaXmlPullProvider>();
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

				using (Logger.BeginScope("定期短期フィード受信"))
				{
					try
					{
						await FetchFeed(false, false);
					}
					catch (Exception ex)
					{
						Logger.LogInformation("短期フィードの受信中に例外が発生しました。\n" + ex);
					}
				}
			});
			Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Assembly.GetExecutingAssembly().GetName().Version};twitter@ingen084");
		}

		private HttpClient Client { get; } = new HttpClient(new HttpClientHandler()
		{
			AutomaticDecompression = DecompressionMethods.All
		});
		private List<(string title, string url, DateTime arrivalTime)> ItemsCache { get; } = new();

		private DateTime LastElapsedTime { get; set; } = DateTime.MinValue;
		private DateTime LastChecked { get; set; } = DateTime.MinValue;

		private DateTimeOffset? LongFeedLastModified { get; set; }
		private DateTimeOffset? ShortFeedLastModified { get; set; }

		private string[] TitleFilter { get; set; } = Array.Empty<string>();

		public bool Enabled { get; private set; } = false;

		public async override Task StartAsync(string[] fetchTitles, string[] fetchTypes)
		{
			if (Enabled)
				return;
			Enabled = true;
			TitleFilter = fetchTitles;
			Logger.LogInformation("JMAXMLを有効化しています。");
			using (Logger.BeginScope("初期フィード受信"))
			{
				// 短期フィードを過去に受信したことがないか、追跡時間を過ぎている場合長期フィードを受信する
				if (ShortFeedLastModified is not DateTimeOffset mod || (DateTimeOffset.Now - mod).TotalMinutes > 10)
				{
					Logger.LogInformation("長期フィード受信中...");
					await FetchFeed(true, true);
				}
				Logger.LogInformation("短期フィード受信中...");
				await FetchFeed(false, true);
			}
			OnInformationSwitched(ItemsCache
				.Select(c =>
					new Information(
						c.url,
						c.title,
						c.arrivalTime,
						() => InformationCacheService.Default.TryGetOrFetchContentAsync(c.url, c.title, c.arrivalTime, () => FetchAsync(c.url))
					))
				.ToArray());
		}

		public override Task StopAsync()
		{
			if (!Enabled)
				return Task.CompletedTask;
			Logger.LogInformation("JMAXMLを無効化しています。");
			ItemsCache.Clear();
			Enabled = false;
			return Task.CompletedTask;
		}

		private async Task FetchFeed(bool useLongFeed, bool supressNotification)
		{
			// TODO: eqvol以外にも対応させる
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
				Logger.LogInformation("JMAXMLフィード - NotModified");
				return;
			}
			Logger.LogInformation($"JMAXMLフィード更新処理開始 Last:{lastModified:yyyy/MM/dd HH:mm:ss} Current:{response.Content.Headers.LastModified:yyyy/MM/dd HH:mm:ss}");

			using var reader = XmlReader.Create(await response.Content.ReadAsStreamAsync());
			var feed = SyndicationFeed.Load(reader);

			// 未処理のものを古いものから列挙
			var matchItems = feed.Items
				.Where(i => !ItemsCache.Any(i2 => i2.url == i.Links.First().GetAbsoluteUri().ToString()) && TitleFilter.Contains(i.Title.Text))
				.OrderBy(i => i.LastUpdatedTime);

			// URLにないものを抽出
			foreach (var item in matchItems)
			{
				Logger.LogTrace($"処理 {item.LastUpdatedTime:yyyy/MM/dd HH:mm:ss} {item.Title.Text}");

				var feedItem = (title: item.Title.Text, url: item.Links.First().GetAbsoluteUri().ToString(), arrivalTime: item.LastUpdatedTime.DateTime);
				ItemsCache.Insert(0, feedItem);
				// 情報補完時(ロングフィード受信時)は処理しない
				if (!supressNotification)
					OnInformationArrived(
						new Information(
							feedItem.url,
							feedItem.title,
							feedItem.arrivalTime,
							() => InformationCacheService.Default.TryGetOrFetchContentAsync(feedItem.url, feedItem.title, feedItem.arrivalTime, () => FetchAsync(feedItem.url))
						));
			}
			if (ItemsCache.Count > 100)
				ItemsCache.RemoveRange(100, ItemsCache.Count - 100);

			if (useLongFeed)
				LongFeedLastModified = response.Content.Headers?.LastModified?.UtcDateTime;
			else
				ShortFeedLastModified = response.Content.Headers?.LastModified?.UtcDateTime;
		}

		private async Task<Stream> FetchAsync(string uri)
		{
			int retry = 0;
			// リトライループ
			while (true)
			{
				Logger.LogInformation($"電文取得中({retry}): {uri}");
				var cresponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));
				if (cresponse.StatusCode != HttpStatusCode.OK)
				{
					retry++;
					if (retry >= 10)
						throw new Exception($"XMLの取得に失敗しました！ Status: {cresponse.StatusCode} Url: {uri}");
					continue;
				}
				return await cresponse.Content.ReadAsStreamAsync();
			}
		}
	}
}
