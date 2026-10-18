using KyoshinEewViewer.Core;
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
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services.TelegramPublishers.JmaXml;

public class JmaXmlTelegramPublisher : TelegramPublisher
{
	private const string DefaultFeedBaseUrl = "https://www.data.jma.go.jp/developer/xml/feed/";
	// 定期取得が連続してこの回数失敗したら、受信できていない状態にする
	private const int MaxPollFailCount = 3;
	// 受信できなくなったあと、再び受信できるかを確認する間隔の上限
	private static readonly TimeSpan MaxRecoveryCheckInterval = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan InitialRecoveryCheckInterval = TimeSpan.FromSeconds(10);

	private ILogger Logger { get; }
	private TimerService Timer { get; }
	private InformationCacheService CacheService { get; }
	private HttpClient Client { get; }
	private TimeProvider TimeProvider { get; }
	private ITimer RecoveryCheckTimer { get; }
	private TimeSpan? RecoveryCheckInterval { get; set; }

	// InformationCategoryに対応するJmaXmlTypeのマップ
	private Dictionary<InformationCategory, JmaXmlType> CategoryMap { get; } = new()
	{
		{ InformationCategory.Earthquake, JmaXmlType.EqVol },
		{ InformationCategory.Tsunami, JmaXmlType.EqVol },
		{ InformationCategory.Typhoon, JmaXmlType.Extra }
	};
	// 受信するJmaXmlTypeに紐づく情報
	private Dictionary<JmaXmlType, (string LongFeed, string ShortFeed)> Feeds { get; }
	// Titleに対するInformationCategoryのマップ
	private Dictionary<string, InformationCategory> TitleMap { get; } = new()
	{
		{ "震度速報", InformationCategory.Earthquake },
		{ "震源に関する情報", InformationCategory.Earthquake },
		{ "震源・震度に関する情報", InformationCategory.Earthquake },
		{ "顕著な地震の震源要素更新のお知らせ", InformationCategory.Earthquake },
		{ "長周期地震動に関する観測情報", InformationCategory.Earthquake },
		{ "津波警報・注意報・予報a", InformationCategory.Tsunami },
		{ "津波情報a", InformationCategory.Tsunami },
		{ "沖合の津波観測に関する情報", InformationCategory.Tsunami },
		{ "台風の暴風域に入る確率", InformationCategory.Typhoon },
		{ "台風解析・予報情報（５日予報）（Ｈ３０）", InformationCategory.Typhoon },
	};
	// 受信中のJmaXmlTypeに関する情報 = 受信中のJmaXmlType
	private ConcurrentDictionary<JmaXmlType, FeedContext> FeedContexts { get; } = new();
	// フィードの取得処理が重複しないようにするためのロック
	private ConcurrentDictionary<JmaXmlType, SemaphoreSlim> FeedLocks { get; } = new();
	// 現在受信中のカテゴリ
	private List<InformationCategory> SubscribingCategories { get; } = [];
	private DateTime LastElapsedTime { get; set; } = DateTime.MinValue;

	public JmaXmlTelegramPublisher(ILogger<JmaXmlTelegramPublisher> logger, TimerService timer, InformationCacheService cacheService)
		: this(logger, timer, cacheService, new HttpClient(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(10) }, DefaultFeedBaseUrl, TimeProvider.System)
	{
	}

	/// <summary>
	/// 取得先と時刻を指定して生成する<br/>
	/// テストで通信先と時刻を差し替えるために使う
	/// </summary>
	/// <param name="feedBaseUrl">フィードのURLの共通部分 (末尾は / )</param>
	internal JmaXmlTelegramPublisher(ILogger<JmaXmlTelegramPublisher> logger, TimerService timer, InformationCacheService cacheService, HttpClient client, string feedBaseUrl, TimeProvider timeProvider)
	{
		Logger = logger;
		Timer = timer;
		CacheService = cacheService;
		Client = client;
		TimeProvider = timeProvider;
		Feeds = new()
		{
			{ JmaXmlType.EqVol, (feedBaseUrl + "eqvol_l.xml", feedBaseUrl + "eqvol.xml") },
			{ JmaXmlType.Extra, (feedBaseUrl + "extra_l.xml", feedBaseUrl + "extra.xml") },
		};
		RecoveryCheckTimer = timeProvider.CreateTimer(_ => _ = CheckRecoveryAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Utils.Version};twitter@ingen084");
		timer.TimerElapsed += async t =>
		{
			if (LastElapsedTime > t)
				return;
			var prev = LastElapsedTime;
			LastElapsedTime = t;
			if (prev.Second != 19 || t.Second != 20) // 毎時20秒から処理開始
				return;
			await PollAsync();
		};
	}

	/// <summary>
	/// 受信中のフィードの短期フィードを取得する<br/>
	/// 通常は毎分20秒に呼ばれる
	/// </summary>
	internal async Task PollAsync()
	{
		foreach (var ctx in FeedContexts.ToArray()) // ループ内で操作があるのでtoArrayしておく
		{
			// 最後の処理から50秒未満であればそのまま終了
			var now = TimeProvider.GetUtcNow().UtcDateTime;
			if (now - ctx.Value.LastFetched < TimeSpan.FromSeconds(50))
				continue;
			ctx.Value.LastFetched = now;

			var feedLock = FeedLocks.GetOrAdd(ctx.Key, _ => new SemaphoreSlim(1, 1));
			// 他のスレッドで処理中ならスキップする
			if (!await feedLock.WaitAsync(0))
			{
				Logger.LogWarning("{Key}の短期フィード受信が他のスレッドで処理中のためスキップされました。", ctx.Key);
				continue;
			}
			try
			{
				// 受信を止めたフィードは取得しない
				if (!FeedContexts.TryGetValue(ctx.Key, out var context) || context != ctx.Value)
					continue;
				await FetchFeedAsync(ctx.Key, context, false, false);
				context.FailCount = 0;
			}
			catch (Exception ex)
			{
				// 一時的な失敗では受信を続け、次の定期取得で取得し直す
				ctx.Value.FailCount++;
				if (ctx.Value.FailCount < MaxPollFailCount)
				{
					Logger.LogWarning(ex, "{Key}の短期フィード受信中にエラーが発生しました (連続{FailCount}回目)", ctx.Key, ctx.Value.FailCount);
					continue;
				}

				// 回線断やタイムアウトも受信できていない状態として通知し、再び受信できるようになったら自動で再開する
				Logger.LogWarning(ex, "{Key}の短期フィードを連続{FailCount}回受信できなかったため、受信できていない状態にします", ctx.Key, ctx.Value.FailCount);
				FeedContexts.Remove(ctx.Key, out _);

				// 現在のFeedTypeにマッチするカテゴリをFailさせる
				OnFailed(CategoryMap.Where(m => m.Value == ctx.Key && SubscribingCategories.Contains(m.Key)).Select(m => m.Key).ToArray(), false);
				ScheduleRecoveryCheck();
			}
			finally
			{
				feedLock.Release();
			}
		}
	}

	// 定期取得のためのタイマーを開始する
	public override Task InitializeAsync()
	{
		Timer.StartMainTimer();
		return Task.CompletedTask;
	}

	private (DateTimeOffset time, InformationCategory[] result)? SupportedCategoryCache { get; set; } = null;
	public async override Task<InformationCategory[]> GetSupportedCategoriesAsync()
	{
		// キャッシュの有効期限は10秒間
		if (SupportedCategoryCache is (DateTimeOffset, not null) cache && cache.time > TimeProvider.GetUtcNow().AddSeconds(-10))
			return cache.result;

		try
		{
			var supportedCategories = await FetchSupportedCategoriesAsync();
			SupportedCategoryCache = (TimeProvider.GetUtcNow(), supportedCategories);
			if (supportedCategories.Length < CategoryMap.Count)
				ScheduleRecoveryCheck();
			return supportedCategories;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "防災情報XMLのフィードを確認できません");
			ScheduleRecoveryCheck();
			return [];
		}
	}

	/// <summary>
	/// HEADリクエストを送信して取得できるフィードのカテゴリを返す
	/// </summary>
	private async Task<InformationCategory[]> FetchSupportedCategoriesAsync()
	{
		var supportedCategories = new List<InformationCategory>();
		foreach (var f in Feeds)
		{
			using var longResp = await Client.SendAsync(new(HttpMethod.Head, f.Value.LongFeed));
			using var shortResp = await Client.SendAsync(new(HttpMethod.Head, f.Value.ShortFeed));
			if (longResp.IsSuccessStatusCode && shortResp.IsSuccessStatusCode)
				supportedCategories.AddRange(CategoryMap.Where(m => m.Value == f.Key).Select(m => m.Key).ToArray());
		}
		return supportedCategories.ToArray();
	}

	/// <summary>
	/// 受信できなくなったため、再び受信できるようになったかの確認を予約する
	/// </summary>
	private void ScheduleRecoveryCheck()
	{
		lock (RecoveryCheckTimer)
		{
			// 予約済みであれば重ねて予約しない
			if (RecoveryCheckInterval != null)
				return;
			RecoveryCheckInterval = InitialRecoveryCheckInterval;
			RecoveryCheckTimer.Change(InitialRecoveryCheckInterval, Timeout.InfiniteTimeSpan);
		}
	}

	private async Task CheckRecoveryAsync()
	{
		try
		{
			var supportedCategories = await FetchSupportedCategoriesAsync();
			SupportedCategoryCache = (TimeProvider.GetUtcNow(), supportedCategories);
			if (supportedCategories.Length >= CategoryMap.Count)
			{
				lock (RecoveryCheckTimer)
					RecoveryCheckInterval = null;
				Logger.LogInformation("防災情報XMLのフィードを再び取得できるようになりました");
				OnInformationCategoryUpdated();
				return;
			}
		}
		catch (Exception ex)
		{
			Logger.LogDebug(ex, "防災情報XMLのフィードはまだ取得できません");
		}

		lock (RecoveryCheckTimer)
		{
			var interval = RecoveryCheckInterval ?? InitialRecoveryCheckInterval;
			RecoveryCheckInterval = interval * 2 < MaxRecoveryCheckInterval ? interval * 2 : MaxRecoveryCheckInterval;
			RecoveryCheckTimer.Change(RecoveryCheckInterval.Value, Timeout.InfiniteTimeSpan);
		}
	}

	public async override void Start(InformationCategory[] categories)
	{
		// 新規追加するもののみ抽出
		var added = categories.Where(c => !SubscribingCategories.Contains(c)).ToArray();
		SubscribingCategories.AddRange(added);

		foreach (var type in added.Select(c => CategoryMap[c]).Distinct().ToArray())
		{
			var feedLock = FeedLocks.GetOrAdd(type, _ => new SemaphoreSlim(1, 1));
			await feedLock.WaitAsync();
			try
			{
				// 既に受信中のフィードであれば、取得済みの電文から追加したカテゴリの履歴を通知する
				if (!FeedContexts.TryGetValue(type, out var context))
				{
					context = new();

					// 長期フィード
					await FetchFeedAsync(type, context, true, true);
					// 短期フィード
					await FetchFeedAsync(type, context, false, true);

					// すぐに無効になった場合とか
					if (!SubscribingCategories.Select(c => CategoryMap[c]).Any(t => t == type))
					{
						Logger.LogWarning("{Type}の取得が完了していましたが、すでに不要になっていたため破棄を行います", type);
						continue;
					}

					// コンテキストを登録 このタイミングからPULL開始
					FeedContexts.TryAdd(type, context);
				}

				// 初期化完了で通知
				foreach (var c in added.Where(c => CategoryMap[c] == type))
					OnHistoryTelegramArrived("防災情報XML", c, context.LatestTelegrams.Where(t => TitleMap.TryGetValue(t.Title, out var cat) && cat == c).OrderBy(t => t.ArrivalTime).ToArray());
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "{Type}の初回フィードの受信中に例外が発生しました", type);
				// 現在のFeedTypeにマッチするカテゴリをFailさせる
				OnFailed(CategoryMap.Where(m => m.Value == type).Select(m => m.Key).ToArray(), false);
				ScheduleRecoveryCheck();
			}
			finally
			{
				feedLock.Release();
			}
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
			Logger.LogDebug("{Type}フィード - NotModified", type);
			return;
		}
		Logger.LogDebug("{Type}フィード更新処理開始 Last:{LastModified:yyyy/MM/dd HH:mm:ss} Current:{LastModified2:yyyy/MM/dd HH:mm:ss}", type, lastModified, response.Content.Headers.LastModified);

		using var reader = XmlReader.Create(await response.Content.ReadAsStreamAsync());
		var feed = SyndicationFeed.Load(reader);

		// 未処理のものを古いものから列挙
		var matchItems = feed.Items
			.Where(i => !context.LatestTelegrams.Any(i2 => i2.Key == i.Links.First().GetAbsoluteUri().ToString()))
			.OrderBy(i => i.LastUpdatedTime);

		// URLにないものを抽出
		foreach (var item in matchItems.ToArray())
		{
			// ロングフィード処理時はログが大量になり重いのでログを出さない
			if (!useLongFeed)
				Logger.LogDebug("処理 {LastUpdatedTime:yyyy/MM/dd HH:mm:ss} {Text}", item.LastUpdatedTime, item.Title.Text);

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
					Logger.LogDebug("HEAD Check {StatusCode}: {Url}", headResponse.StatusCode, url);
				}
				catch (Exception ex)
				{
					// HEADに失敗した場合は終了する
					Logger.LogWarning(ex, "電文のHEADリクエストに失敗しました");
					return;
				}
			}

			var telegram = new JmaTelegram(
				this,
				url,
				title,
				url,
				item.LastUpdatedTime.DateTime
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
			Logger.LogInformation("電文取得中({Retry}): {Uri}", retry, uri);
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
	public class JmaTelegram(
		JmaXmlTelegramPublisher publisher,
		string url,
		string title,
		string rawId,
		DateTime arrivalTime
	) : Telegram(url, title, rawId, arrivalTime)
	{
		public override Task<Stream> GetBodyAsync() => publisher.CacheService.TryGetOrFetchTelegramAsync(Key, () => publisher.FetchAsync(Key));
		public override void Cleanup() => publisher.CacheService.DeleteTelegramCache(Key);
	}
}
