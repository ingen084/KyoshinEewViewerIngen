using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services;

public class TelegramProvideService : IDisposable
{
	/// <summary>
	/// デフォルトのPublisher Types（優先度順）
	/// </summary>
	public static Type[] DefaultPublisherTypes { get; } =
	[
		typeof(DmdataRedundantTelegramPublisher),
		typeof(JmaXmlTelegramPublisher),
	];

	/// <summary>
	/// publisher 上にある方が優先度が高い
	/// </summary>
	private List<TelegramPublisher> Publishers { get; } = [];
	private Dictionary<InformationCategory, List<Subscriber>> Subscribers { get; } = new()
	{
		{ InformationCategory.Earthquake, new() },
		{ InformationCategory.EewForecast, new() },
		{ InformationCategory.EewWarning, new() },
		{ InformationCategory.Tsunami, new() },
		{ InformationCategory.Typhoon, new() },
	};
	private Dictionary<InformationCategory, TelegramPublisher?> UsingPublisher { get; } = [];
	
	// 失敗処理の同時実行制御用
	private readonly SemaphoreSlim _failureProcessingSemaphore = new(1, 1);
	private readonly HashSet<(TelegramPublisher, InformationCategory)> _processingFailures = [];

	private ILogger Logger { get; }
	private IServiceProvider Services { get; }

	public TelegramProvideService(ILogger<TelegramProvideService> logger, IServiceProvider services)
	{
		Services = services;
		Logger = logger;
	}

	private bool Started { get; set; } = false;
	/// <summary>
	/// 開始する
	/// </summary>
	/// <param name="publisherTypes">使用するPublisherのType配列（nullの場合はデフォルト）</param>
	public async Task StartAsync(Type[]? publisherTypes = null)
	{
		if (Started)
			throw new InvalidOperationException("すでに開始しています");
		Started = true;

		// 引数で指定されたType配列、またはデフォルトを使用
		var typesToUse = publisherTypes ?? DefaultPublisherTypes;

		// Type配列から順番にPublisherを取得して初期化
		foreach (var type in typesToUse)
		{
			TelegramPublisher? publisher = null;
			try
			{
				// ServiceProviderから取得を試みる
				publisher = Services.GetService(type) as TelegramPublisher;

				if (publisher == null)
				{
					Logger.LogWarning("Publisher {Name} をDIコンテナから取得できませんでした", type.Name);
					continue;
				}

				// イベントハンドラの登録
				publisher.HistoryTelegramArrived += OnHistoryTelegramArrived;
				publisher.TelegramArrived += OnTelegramArrived;
				publisher.Failed += OnFailed;
				publisher.InformationCategoryUpdated += OnInformationCategoryUpdated;

				// Publisher固有の初期化
				await publisher.InitializeAsync();
				Publishers.Add(publisher);
				Logger.LogInformation("Publisher {Name} を初期化しました（優先度: {Count}）", type.Name, Publishers.Count);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Publisher {Name} の初期化に失敗しました", type.Name);
			}
		}

		// 割り当てられていないカテゴリたち
		var remainCategories = Subscribers.Where(s => s.Value.Count != 0).Select(s => s.Key).ToList();
		foreach (var publisher in Publishers)
		{
			try
			{
				var supported = await publisher.GetSupportedCategoriesAsync();
				var matched = supported.Where(s => remainCategories.Contains(s)).ToArray();
				if (matched.Length == 0)
					continue;

				// 割当
				foreach (var mc in matched)
					UsingPublisher[mc] = publisher;
				// 開始
				publisher.Start(matched);

				remainCategories.RemoveAll(c => supported.Contains(c));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "電文プロバイダ {Name} の初期化中に例外が発生しました。", publisher.GetType().Name);
			}
		}

		// 処理されなかったカテゴリについて失敗報告
		if (remainCategories.Count <= 0)
			return;

		Logger.LogWarning("以下のカテゴリに対応するPublisherが見つかりませんでした: {RemainCategories}", string.Join(", ", remainCategories));
		foreach (var category in remainCategories)
		{
			foreach (var subscriber in Subscribers[category])
			{
				try
				{
					subscriber.Failed((isAllFailed: true, isRestorable: false));
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "カテゴリ {Category} のサブスクライバーへの失敗報告中に例外が発生しました", category);
				}
			}
		}
	}

	private async void OnHistoryTelegramArrived(TelegramPublisher sender, string name, InformationCategory category, Telegram[] telegrams)
	{
		// 現在利用中のプロバイダからでなければ無視
		if (!UsingPublisher.TryGetValue(category, out var up) || up != sender)
			return;

		foreach (var s in Subscribers[category])
		{
			try
			{
				await s.SourceSwitched(name, telegrams);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "電文プロバイダ {Name} の SourceSwitched 時にサブスクライバーで例外が発生しました。", sender.GetType().Name);
			}
		}
	}
	private async void OnTelegramArrived(TelegramPublisher sender, InformationCategory category, Telegram telegram)
	{
		// 現在利用中のプロバイダからでなければ無視
		if (!UsingPublisher.TryGetValue(category, out var up) || up != sender)
			return;

		foreach (var s in Subscribers[category])
		{
			try
			{
				await s.Arrived(telegram);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "電文プロバイダ {Name} の Arrived 時にサブスクライバーで例外が発生しました。", sender.GetType().Name);
			}
		}
	}

	private void OnFailed(TelegramPublisher sender, InformationCategory[] categories, bool isRestorable)
		=> Task.Run(async () =>
		{
			try
			{
				// 同時実行制御
				await _failureProcessingSemaphore.WaitAsync();
				try
				{
					// 既に処理中の失敗は重複処理を防ぐ
					var newFailures = new List<InformationCategory>();
					foreach (var category in categories)
					{
						var key = (sender, category);
						if (!_processingFailures.Contains(key))
						{
							_processingFailures.Add(key);
							newFailures.Add(category);
						}
					}

					if (newFailures.Count == 0)
					{
						Logger.LogDebug("プロバイダ {Name} の失敗は既に処理中のため、重複処理をスキップします", sender.GetType().Name);
						return;
					}

					categories = newFailures.ToArray();
				}
				finally
				{
					_failureProcessingSemaphore.Release();
				}

				// 使用中のもののみフォールバックできるようにする
				var fallTargetCategories = new List<InformationCategory>();
				foreach (var category in categories)
				{
					// 現在利用中のプロバイダからでなければ無視
					if (!UsingPublisher.TryGetValue(category, out var up) || up != sender)
						continue;

					// Failed通知を送信、フロントは操作不能になる
					foreach (var s in Subscribers[category])
						s.Failed((false, isRestorable));
					fallTargetCategories.Add(category);
				}

				// リストア可能もしくは対象が存在しなければ何もしない
				if (isRestorable || fallTargetCategories.Count == 0)
					return;

				var matchedPublishers = new Dictionary<TelegramPublisher, List<InformationCategory>>();
				foreach (var category in fallTargetCategories)
				{
					var nextPublisher = sender;
					while (true)
					{
						var i = Publishers.IndexOf(nextPublisher);
						// 次に優先度の高いプロバイダに切り替える
						if (i >= (Publishers.Count - 1))
						{
							// フォールバック先が存在しない
							UsingPublisher.Remove(category);
							foreach (var s in Subscribers[category])
								s.Failed((true, false));
							break;
						}
						nextPublisher = Publishers[i + 1];

						try
						{
							if (!(await nextPublisher.GetSupportedCategoriesAsync()).Contains(category))
								continue;
							if (!matchedPublishers.ContainsKey(nextPublisher))
								matchedPublishers.Add(nextPublisher, []);
							matchedPublishers[nextPublisher].Add(category);
							break;
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex, "取得失敗による情報ソース切り替え中に例外が発生しました");
						}
					}
				}
				foreach (var p in matchedPublishers)
				{
					foreach (var c in p.Value)
						UsingPublisher[c] = p.Key;
					p.Key.Start(p.Value.ToArray());
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "情報ソース切り替え中に例外が発生しました");
			}
			finally
			{
				// 処理中フラグをクリア
				try
				{
					await _failureProcessingSemaphore.WaitAsync();
					try
					{
						foreach (var category in categories)
							_processingFailures.Remove((sender, category));
					}
					finally
					{
						_failureProcessingSemaphore.Release();
					}
				}
				catch (Exception cleanupEx)
				{
					Logger.LogError(cleanupEx, "失敗処理のクリーンアップ中に例外が発生しました");
				}
			}
		});

	private void OnInformationCategoryUpdated(TelegramPublisher sender)
		=> Task.Run(async () =>
		{
			try
			{
				var stops = new Dictionary<TelegramPublisher, List<InformationCategory>>();

				// 再計算する - 優先度ベースでPublisherを再配置
				var remainCategories = Subscribers.Where(s => s.Value.Count != 0).Select(s => s.Key).ToList();
				foreach (var publisher in Publishers)
				{
					try
					{
						var supported = await publisher.GetSupportedCategoriesAsync();
						var matched = supported.Where(s => remainCategories.Contains(s));
						if (!matched.Any())
							continue;

						// より優先度の高いPublisherが復旧した場合、現在のPublisherから切り替える
						var toReassign = new List<InformationCategory>();
						foreach (var category in matched)
						{
							if (UsingPublisher.TryGetValue(category, out var currentPublisher) && currentPublisher != null)
							{
								// 現在のPublisherより優先度が高い場合（Publishersリストの先頭に近い）
								var currentIndex = Publishers.IndexOf(currentPublisher);
								var newIndex = Publishers.IndexOf(publisher);
								if (newIndex < currentIndex)
								{
									toReassign.Add(category);
									Logger.LogInformation("カテゴリ {Category} を {Name} から {Name2} に切り替えます（優先度による復旧）", category, currentPublisher.GetType().Name, publisher.GetType().Name);
								}
							}
							else
							{
								// 現在割り当てられていない場合
								toReassign.Add(category);
								Logger.LogInformation("カテゴリ {Category} を {Name} に割り当てます", category, publisher.GetType().Name);
							}
						}

						// 切り替え処理
						foreach (var category in toReassign)
						{
							if (UsingPublisher.TryGetValue(category, out var oldPublisher) && oldPublisher != null)
							{
								if (!stops.ContainsKey(oldPublisher))
									stops.Add(oldPublisher, []);
								stops[oldPublisher].Add(category);
							}
							UsingPublisher[category] = publisher;
						}

						// 開始
						if (toReassign.Count != 0)
							publisher.Start(toReassign.ToArray());

						remainCategories.RemoveAll(c => supported.Contains(c));
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "電文プロバイダ {Name} の情報カテゴリ更新中に例外が発生しました。", publisher.GetType().Name);
					}
				}

				// 停止させる
				foreach (var s in stops)
				{
					Logger.LogInformation("{Publisher} の以下のカテゴリを停止します: {Categories}", s.Key.GetType().Name, string.Join(", ", s.Value));
					s.Key.Stop(s.Value.ToArray());
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "情報カテゴリ更新処理中に例外が発生しました");
			}
		});

	public async Task RestoreAsync()
	{
		try
		{
			Logger.LogInformation("電文プロバイダの復旧処理を開始します");
			
			// 復旧対象のカテゴリを収集
			// 1. 割り当てられていないカテゴリ
			// 2. 現在のPublisherがサポートしていないカテゴリ（障害状態など）
			var categoriesToRestore = new List<InformationCategory>();
			
			foreach (var subscriber in Subscribers.Where(s => s.Value.Count != 0))
			{
				var category = subscriber.Key;
				var needsRestore = false;
				
				if (!UsingPublisher.TryGetValue(category, out var currentPublisher) || currentPublisher == null)
				{
					// 割り当てられていない
					needsRestore = true;
					Logger.LogDebug("カテゴリ {Category} は割り当てられていません", category);
				}
				else
				{
					// 現在のPublisherがサポートしているかチェック
					try
					{
						var supportedCategories = await currentPublisher.GetSupportedCategoriesAsync();
						if (!supportedCategories.Contains(category))
						{
							needsRestore = true;
							Logger.LogInformation("カテゴリ {Category} は現在のプロバイダ {Name} でサポートされていないため復旧対象とします", category, currentPublisher.GetType().Name);
						}
					}
					catch (Exception ex)
					{
						Logger.LogWarning(ex, "プロバイダ {Name} のサポートカテゴリ取得に失敗したため復旧対象とします", currentPublisher.GetType().Name);
						needsRestore = true;
					}
				}
				
				if (needsRestore)
					categoriesToRestore.Add(category);
			}

			if (categoriesToRestore.Count == 0)
			{
				Logger.LogInformation("復旧対象のカテゴリは見つかりませんでした");
				return;
			}

			Logger.LogInformation("復旧対象カテゴリ: {CategoriesToRestore}", string.Join(", ", categoriesToRestore));

			// 優先度順でPublisherを試行
			var stops = new Dictionary<TelegramPublisher, List<InformationCategory>>();
			foreach (var publisher in Publishers)
			{
				try
				{
					var supported = await publisher.GetSupportedCategoriesAsync();
					var matched = supported.Where(categoriesToRestore.Contains).ToArray();
					if (matched.Length == 0)
						continue;

					Logger.LogInformation("{Name} で {Matched} を復旧します", publisher.GetType().Name, string.Join(", ", matched));

					// 既存のPublisherを停止リストに追加
					foreach (var category in matched)
					{
						if (UsingPublisher.TryGetValue(category, out var oldPublisher) && oldPublisher != null && oldPublisher != publisher)
						{
							if (!stops.ContainsKey(oldPublisher))
								stops[oldPublisher] = [];
							stops[oldPublisher].Add(category);
						}
						UsingPublisher[category] = publisher;
					}

					// 開始
					publisher.Start(matched);
					categoriesToRestore.RemoveAll(matched.Contains);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "電文プロバイダ {Name} の復旧処理中に例外が発生しました。", publisher.GetType().Name);
				}
			}

			// 古いPublisherを停止
			foreach (var stop in stops)
			{
				Logger.LogInformation("{Publisher} の以下のカテゴリを停止します: {Categories}", stop.Key.GetType().Name, string.Join(", ", stop.Value));
				stop.Key.Stop(stop.Value.ToArray());
			}

			if (categoriesToRestore.Count > 0)
			{
				Logger.LogWarning("復旧できなかったカテゴリ: {CategoriesToRestore}", string.Join(", ", categoriesToRestore));
			}
			else
			{
				Logger.LogInformation("全てのカテゴリの復旧が完了しました");
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "復旧処理中に予期しない例外が発生しました");
		}
	}

	/// <summary>
	/// 購読を開始する
	/// <para>開始(Start)後は受信できない</para>
	/// </summary>
	/// <param name="category">対象のカテゴリ</param>
	/// <param name="sourceSwitched">購読開始時･情報ソース変更時に呼ばれる</param>
	/// <param name="arrived">情報受信時に呼ばれる</param>
	/// <param name="failed">ソース失効時に呼ばれる</param>
	public void Subscribe(
		InformationCategory category,
		Func<string, Telegram[], Task> sourceSwitched,
		Func<Telegram, Task> arrived,
		Action<(bool isAllFailed, bool isRestorable)> failed)
	{
		if (Started)
			throw new InvalidOperationException("開始後の購読開始はできません。");
		if (!Subscribers.TryGetValue(category, out var subscribers))
			return;
		var subscriver = new Subscriber(sourceSwitched, arrived, failed);
		subscribers.Add(subscriver);
	}

	public void Dispose()
	{
		foreach (var publisher in Publishers)
		{
			try
			{
				publisher.HistoryTelegramArrived -= OnHistoryTelegramArrived;
				publisher.TelegramArrived -= OnTelegramArrived;
				publisher.Failed -= OnFailed;
				publisher.InformationCategoryUpdated -= OnInformationCategoryUpdated;
				// TelegramPublisherにはDisposeメソッドがない
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Publisher {Name} の破棄中に例外が発生しました", publisher.GetType().Name);
			}
		}
		Publishers.Clear();
		UsingPublisher.Clear();
		Started = false;
		
		// セマフォの破棄
		_failureProcessingSemaphore?.Dispose();
		
		GC.SuppressFinalize(this);
	}

	private sealed record Subscriber(Func<string, Telegram[], Task> SourceSwitched, Func<Telegram, Task> Arrived, Action<(bool isAllFailed, bool isRestorable)> Failed);
}

/// <summary>
/// 内部で情報を受信する際に使用するカテゴリ
/// </summary>
public enum InformationCategory
{
	/// <summary>
	/// 地震情報
	/// </summary>
	Earthquake,
	/// <summary>
	/// 津波
	/// </summary>
	Tsunami,
	/// <summary>
	/// 緊急地震速報(予報)
	/// </summary>
	EewForecast,
	/// <summary>
	/// 緊急地震速報(警報)
	/// </summary>
	EewWarning,
	/// <summary>
	/// 台風情報
	/// </summary>
	Typhoon,
}
