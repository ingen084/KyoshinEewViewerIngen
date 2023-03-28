using KyoshinEewViewer.Core;
using Splat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace KyoshinEewViewer.Services.TelegramPublishers.JmaXml;

public class JmaXmlTelegramPublisher : TelegramPublisher
{
	private ILogger Logger { get; }
	private TimerService Timer { get; }
	private InformationCacheService CacheService { get; }
	private HttpClient Client { get; } = new(new HttpClientHandler()
	{
		AutomaticDecompression = DecompressionMethods.All
	})
	{
		Timeout = TimeSpan.FromSeconds(10),
	};

	// InformationCategoryに対応するJmaXmlTypeのマップ
	private Dictionary<InformationCategory, JmaXmlType> CategoryMap { get; } = new()
	{
		{ InformationCategory.Earthquake, JmaXmlType.EqVol },
		{ InformationCategory.Tsunami, JmaXmlType.EqVol },
		{ InformationCategory.Typhoon, JmaXmlType.Extra }
	};
	// 受信するJmaXmlTypeに紐づく情報
	private Dictionary<JmaXmlType, (string LongFeed, string ShortFeed)> Feeds { get; } = new()
	{
		{
			JmaXmlType.EqVol,
			(
				"https://www.data.jma.go.jp/developer/xml/feed/eqvol_l.xml",
				"https://www.data.jma.go.jp/developer/xml/feed/eqvol.xml"
			)
		},
		{
			JmaXmlType.Extra,
			(
				"https://www.data.jma.go.jp/developer/xml/feed/extra_l.xml",
				"https://www.data.jma.go.jp/developer/xml/feed/extra.xml"
			)
		}
	};
	// Titleに対するInformationCategoryのマップ
	private Dictionary<string, InformationCategory> TitleMap { get; } = new()
	{
		{ "震度速報", InformationCategory.Earthquake },
		{ "震源に関する情報", InformationCategory.Earthquake },
		{ "震源・震度に関する情報", InformationCategory.Earthquake },
		{ "顕著な地震の震源要素更新のお知らせ", InformationCategory.Earthquake },
		{ "津波警報・注意報・予報a", InformationCategory.Tsunami },
		{ "津波情報a", InformationCategory.Tsunami },
		{ "沖合の津波観測に関する情報", InformationCategory.Tsunami },
		{ "台風の暴風域に入る確率", InformationCategory.Typhoon },
		{ "台風解析・予報情報（５日予報）（Ｈ３０）", InformationCategory.Typhoon },
	};
	// 受信中のJmaXmlTypeに関する情報 = 受信中のJmaXmlType
	private ConcurrentDictionary<JmaXmlType, FeedContext> FeedContexts { get; } = new();
	// 情報取得時処理が重複しないようにするためのMRE
	private ConcurrentDictionary<JmaXmlType, ManualResetEventSlim> FeedResetEvents { get; } = new();
	// 現在受信中のカテゴリ
	private List<InformationCategory> SubscribingCategories { get; } = new();
	private DateTime LastElapsedTime { get; set; } = DateTime.MinValue;

	public JmaXmlTelegramPublisher(ILogger logger, TimerService timer, InformationCacheService cacheService)
	{
		SplatRegistrations.RegisterLazySingleton<JmaXmlTelegramPublisher>();

		Logger = logger;
		Timer = timer;
		CacheService = cacheService;

		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Utils.Version};twitter@ingen084");
		timer.TimerElapsed += async t =>
		{
			if (LastElapsedTime > t)
				return;
			var prev = LastElapsedTime;
			LastElapsedTime = t;
			if (prev.Second != 19 || t.Second != 20) // 毎時20秒から処理開始
				return;

			foreach (var ctx in FeedContexts.ToArray()) // ループ内で操作があるのでtoArrayしておく
			{
				// 最後の処理から50秒未満であればそのまま終了
				if (DateTime.UtcNow - ctx.Value.LastFetched < TimeSpan.FromSeconds(50))
					continue;
				ctx.Value.LastFetched = DateTime.UtcNow;

				try
					{
						// 初回取得処理
						if (!FeedResetEvents.TryGetValue(ctx.Key, out var mre))
						{
							Logger.LogError($"{ctx.Key}のMREが取得できません。");
							continue;
						}
						// 他のスレッドで処理中なら待機してメソッド自体の実行し直し
						if (!mre.IsSet)
						{
							Logger.LogWarning($"{ctx.Key}の短期フィード受信が他のスレッドで処理中のためスキップされました。");
							continue;
						}
						mre.Reset();
						try
						{
							await FetchFeedAsync(ctx.Key, ctx.Value, false, false);
						}
						catch (HttpRequestException ex)
						{
							// HTTPエラー
							Logger.LogWarning(ex, $"{ctx.Key}の短期フィード受信中にHTTPエラーが発生しました");
						}
						catch (TaskCanceledException ex)
						{
							// タイムアウト
							Logger.LogWarning(ex, $"{ctx.Key}の短期フィード受信にタイムアウトしました");
						}
						catch (HeadFetchErrorException ex)
						{
							// HEADが取得できない
							Logger.LogWarning(ex, $"{ctx.Key}の短期フィード内アイテムのHEADに失敗しました");
						}
						catch (XmlException ex)
						{
							// フィードのパースエラー
							Logger.LogWarning(ex, $"{ctx.Key}の短期フィードのパースに失敗しました");
						}
						catch (Exception ex)
						{
							Logger.LogError(ex, $"{ctx.Key}の短期フィード受信中に例外が発生しました");
							FeedContexts.Remove(ctx.Key, out _);

							// 現在のFeedTypeにマッチするカテゴリをFailさせる
							OnFailed(CategoryMap.Where(m => m.Value == ctx.Key && SubscribingCategories.Contains(m.Key)).Select(m => m.Key).ToArray(), false);
						}
						finally
						{
							mre.Set();
						}
					}
					catch (Exception ex)
					{
						Logger.LogInfo(ex, $"{ctx.Key}の短期フィードの受信中に例外が発生しました");
					}
			}
		};
	}

	// 定期取得のためのタイマーを開始する
	public override Task InitalizeAsync()
	{
		Timer.StartMainTimer();
		return Task.CompletedTask;
	}

	private (DateTime time, InformationCategory[] result)? SupportedCategoryCache { get; set; } = null;
	public async override Task<InformationCategory[]> GetSupportedCategoriesAsync()
	{
		// キャッシュの有効期限は10秒間
		if (SupportedCategoryCache is (DateTime, InformationCategory[]) cache && cache.time > DateTime.Now.AddSeconds(-10))
			return cache.result;

		// HEADリクエストを送信して取得できる場合のみサポート対象とする
		var supportedCategories = new List<InformationCategory>();
		foreach (var f in Feeds)
		{
			using var longResp = await Client.SendAsync(new(HttpMethod.Head, f.Value.LongFeed));
			using var shortResp = await Client.SendAsync(new(HttpMethod.Head, f.Value.ShortFeed));
			if (longResp.IsSuccessStatusCode && shortResp.IsSuccessStatusCode)
				supportedCategories.AddRange(CategoryMap.Where(m => m.Value == f.Key).Select(m => m.Key));
		}
		SupportedCategoryCache = (DateTime.Now, supportedCategories.ToArray());
		return supportedCategories.ToArray();
	}

	public async override void Start(InformationCategory[] categories)
	{
		// 新規追加するもののみ抽出
		var added = categories.Where(c => !SubscribingCategories.Contains(c)).ToArray();
		SubscribingCategories.AddRange(added);

		foreach (var type in added.Select(c => CategoryMap[c]).Distinct())
		{
			async Task InitalPullAsync()
			{
				// 初回取得処理
				if (!FeedResetEvents.TryGetValue(type, out var mre))
				{
					mre = new ManualResetEventSlim(true);
					// add失敗であれば他のスレッドで進行中なのでメソッド自体を実行し直す
					if (!FeedResetEvents.TryAdd(type, mre))
					{
						await InitalPullAsync();
						return;
					}
				}
				// 他のスレッドで処理中なら待機してメソッド自体の実行し直し
				if (!mre.IsSet)
				{
					mre.Wait();
					await InitalPullAsync();
					return;
				}
				mre.Reset();
				try
				{
					// コンテキストを取得 存在する場合は初期化済みなのでそのまま戻る
					if (FeedContexts.TryGetValue(type, out var context))
						return;

					// 存在しない場合は作成する
					context = new();

					// 長期フィード
					await FetchFeedAsync(type, context, true, true);
					// 短期フィード
					await FetchFeedAsync(type, context, false, true);

					// すぐに無効になった場合とか
					if (!SubscribingCategories.Select(c => CategoryMap[c]).Any(t => t == type))
					{
						Logger.LogWarning($"{type}の取得が完了していましたが、すでに不要になっていたため破棄を行います");
						return;
					}

					var telegramGroups = new Dictionary<InformationCategory, Telegram[]>();
					foreach (var c in categories)
						telegramGroups[c] = context.LatestTelegrams.Where(t => TitleMap.TryGetValue(t.Title, out var cat) && cat == c).OrderBy(t => t.ArrivalTime).ToArray();

					// 初期化完了で通知
					foreach (var c in telegramGroups.Keys)
						OnHistoryTelegramArrived("防災情報XML", c, telegramGroups[c]);

					// コンテキストを登録 このタイミングからPULL開始
					FeedContexts.TryAdd(type, context);
				}
				catch (Exception ex)
				{
					Logger.LogInfo(ex, $"{type}の初回フィードの受信中に例外が発生しました");
					// 現在のFeedTypeにマッチするカテゴリをFailさせる
					OnFailed(CategoryMap.Where(m => m.Value == type).Select(m => m.Key).ToArray(), false);
				}
				finally
				{
					mre.Set();
				}
			}
			await InitalPullAsync();
		}
	}

	public override void Stop(InformationCategory[] categories)
	{
		SubscribingCategories.RemoveAll(c => categories.Contains(c));

		// 現在使用しているカテゴリをリストアップ
		var currentNeedTypes = SubscribingCategories.Select(c => CategoryMap[c]).ToArray();
		// 現在取得しているフィードと合わせて不要なものを出す
		var deletedTypes = FeedContexts.Where(t => !currentNeedTypes.Any(n => n == t.Key)).ToArray();
		// 不要なものを削除
		foreach (var type in deletedTypes)
			FeedContexts.TryRemove(type);
	}

	/// <summary>
	/// フィードから電文を抜き出す
	/// </summary>
	private async Task FetchFeedAsync(JmaXmlType type, FeedContext context, bool useLongFeed, bool supressNotification)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, useLongFeed ? Feeds[type].LongFeed : Feeds[type].ShortFeed);

		DateTimeOffset? lastModified;
		if (useLongFeed)
			lastModified = context.LongFeedLastModified;
		else
			lastModified = context.ShortFeedLastModified;

		// 初回取得じゃない場合チェックしてもらう
		if (lastModified != null)
			request.Headers.IfModifiedSince = lastModified;
		using var response = await Client.SendAsync(request);
		if (response.StatusCode == HttpStatusCode.NotModified)
		{
			Logger.LogDebug($"{type}フィード - NotModified");
			return;
		}
		Logger.LogDebug($"{type}フィード更新処理開始 Last:{lastModified:yyyy/MM/dd HH:mm:ss} Current:{response.Content.Headers.LastModified:yyyy/MM/dd HH:mm:ss}");

		using var reader = XmlReader.Create(await response.Content.ReadAsStreamAsync());
		var feed = SyndicationFeed.Load(reader);

		// 未処理のものを古いものから列挙
		var matchItems = feed.Items
			.Where(i => !context.LatestTelegrams.Any(i2 => i2.Key == i.Links.First().GetAbsoluteUri().ToString()))
			.OrderBy(i => i.LastUpdatedTime);

		// URLにないものを抽出
		foreach (var item in matchItems)
		{
			// ロングフィード処理時はログが大量になり重いのでログを出さない
			if (!useLongFeed)
				Logger.LogDebug($"処理 {item.LastUpdatedTime:yyyy/MM/dd HH:mm:ss} {item.Title.Text}");

			var url = item.Links.First().GetAbsoluteUri().ToString();
			var title = item.Title.Text;

			// 短期フィードのみかつキャッシュが存在しない場合ファイルが存在することを確認する
			if (!useLongFeed && !CacheService.ExistsTelegramCache(url))
			{
				try
				{
					using var headResponse = await Client.SendAsync(new(HttpMethod.Head, url));
					if (!headResponse.IsSuccessStatusCode)
						throw new HeadFetchErrorException("Status:" + headResponse.StatusCode);
					Logger.LogDebug($"HEAD Check {headResponse.StatusCode}: {url}");
				}
				catch (Exception ex)
				{
					// HEADに失敗した場合は終了する
					Logger.LogWarning(ex, "電文のHEADリクエストに失敗しました");
					return;
				}
			}

			var telegram = new Telegram(
				url,
				title,
				url,
				item.LastUpdatedTime.DateTime,
				() => CacheService.TryGetOrFetchTelegramAsync(url, () => FetchAsync(url)),
				() => CacheService.DeleteTelegramCache(url)
			);

			context.LatestTelegrams.Insert(0, telegram);
			// 情報補完時(ロングフィード受信時)は処理しない
			if (!supressNotification && !useLongFeed && TitleMap.TryGetValue(title, out var cat) && SubscribingCategories.Contains(cat))
				OnTelegramArrived(cat, telegram);
		}
		if (context.LatestTelegrams.Count > 1000)
			context.LatestTelegrams.RemoveRange(1000, context.LatestTelegrams.Count - 1000);

		if (useLongFeed)
			context.LongFeedLastModified = response.Content.Headers?.LastModified?.UtcDateTime;
		else
			context.ShortFeedLastModified = response.Content.Headers?.LastModified?.UtcDateTime;
	}

	private async Task<Stream> FetchAsync(string uri)
	{
		var retry = 0;
		// リトライループ
		while (true)
		{
			Logger.LogInfo($"電文取得中({retry}): {uri}");
			var cresponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));
			if (cresponse.StatusCode != HttpStatusCode.OK)
			{
				await Task.Delay(200);
				retry++;
				if (retry >= 10)
					throw new TelegramFetchFailedException($"XMLの取得に失敗しました！ Status:{cresponse.StatusCode} Url:{uri}");
				continue;
			}
			return await cresponse.Content.ReadAsStreamAsync();
		}
	}
}
