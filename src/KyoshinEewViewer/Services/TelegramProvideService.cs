using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;

public class TelegramProvideService
{
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

	private ILogger Logger { get; }
	private DmdataRedundantTelegramPublisher Dmdata { get; }
	private JmaXmlTelegramPublisher Jma { get; }

	public TelegramProvideService(ILogManager logManager, DmdataRedundantTelegramPublisher dmdata, JmaXmlTelegramPublisher jma)
	{
		SplatRegistrations.RegisterLazySingleton<TelegramProvideService>();

		Logger = logManager.GetLogger<TelegramProvideService>();
		Dmdata = dmdata;
		Jma = jma;	
	}

	private bool Started { get; set; } = false;
	/// <summary>
	/// 開始する
	/// </summary>
	public async Task StartAsync()
	{
		if (Started)
			throw new InvalidOperationException("すでに開始しています");
		Started = true;

		Dmdata.HistoryTelegramArrived += OnHistoryTelegramArrived;
		Dmdata.TelegramArrived += OnTelegramArrived;
		Dmdata.Failed += OnFailed;
		Dmdata.InformationCategoryUpdated += OnInformationCategoryUpdated;
		await Dmdata.InitializeAsync();
		Publishers.Add(Dmdata);

		Jma.HistoryTelegramArrived += OnHistoryTelegramArrived;
		Jma.TelegramArrived += OnTelegramArrived;
		Jma.Failed += OnFailed;
		Jma.InformationCategoryUpdated += OnInformationCategoryUpdated;
		await Jma.InitializeAsync();
		Publishers.Add(Jma);

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
				Logger.LogError(ex, $"電文プロバイダ {publisher.GetType().Name} の初期化中に例外が発生しました。");
			}
		}
	}

	private async void OnHistoryTelegramArrived(TelegramPublisher sender, string name, InformationCategory category, Telegram[] telegrams)
	{
		// 現在利用中のプロバイダからでなければ無視
		if (!UsingPublisher.TryGetValue(category, out var up) || up != sender)
			return;

		try
		{
			foreach (var s in Subscribers[category])
				await s.SourceSwitched(name, telegrams);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, $"電文プロバイダ {sender.GetType().Name} の SourceSwitched 時に例外が発生しました。");
		}
	}
	private async void OnTelegramArrived(TelegramPublisher sender, InformationCategory category, Telegram telegram)
	{
		// 現在利用中のプロバイダからでなければ無視
		if (!UsingPublisher.TryGetValue(category, out var up) || up != sender)
			return;

		try
		{
			foreach (var s in Subscribers[category])
				await s.Arrived(telegram);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, $"電文プロバイダ {sender.GetType().Name} の Arrived 時に例外が発生しました。");
		}
	}

	private void OnFailed(TelegramPublisher sender, InformationCategory[] categories, bool isRestorable)
		=> Task.Run(async () =>
		{
			try
			{
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
									Logger.LogInfo($"カテゴリ {category} を {currentPublisher.GetType().Name} から {publisher.GetType().Name} に切り替えます（優先度による復旧）");
								}
							}
							else
							{
								// 現在割り当てられていない場合
								toReassign.Add(category);
								Logger.LogInfo($"カテゴリ {category} を {publisher.GetType().Name} に割り当てます");
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
						Logger.LogError(ex, $"電文プロバイダ {publisher.GetType().Name} の情報カテゴリ更新中に例外が発生しました。");
					}
				}

				// 停止させる
				foreach (var s in stops)
				{
					Logger.LogInfo($"{s.Key.GetType().Name} の以下のカテゴリを停止します: {string.Join(", ", s.Value)}");
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
			Logger.LogInfo("電文プロバイダの復旧処理を開始します");
			
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
					Logger.LogDebug($"カテゴリ {category} は割り当てられていません");
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
							Logger.LogInfo($"カテゴリ {category} は現在のプロバイダ {currentPublisher.GetType().Name} でサポートされていないため復旧対象とします");
						}
					}
					catch (Exception ex)
					{
						Logger.LogWarning(ex, $"プロバイダ {currentPublisher.GetType().Name} のサポートカテゴリ取得に失敗したため復旧対象とします");
						needsRestore = true;
					}
				}
				
				if (needsRestore)
					categoriesToRestore.Add(category);
			}

			if (categoriesToRestore.Count == 0)
			{
				Logger.LogInfo("復旧対象のカテゴリは見つかりませんでした");
				return;
			}

			Logger.LogInfo($"復旧対象カテゴリ: {string.Join(", ", categoriesToRestore)}");

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

					Logger.LogInfo($"{publisher.GetType().Name} で {string.Join(", ", matched)} を復旧します");

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
					Logger.LogError(ex, $"電文プロバイダ {publisher.GetType().Name} の復旧処理中に例外が発生しました。");
				}
			}

			// 古いPublisherを停止
			foreach (var stop in stops)
			{
				Logger.LogInfo($"{stop.Key.GetType().Name} の以下のカテゴリを停止します: {string.Join(", ", stop.Value)}");
				stop.Key.Stop(stop.Value.ToArray());
			}

			if (categoriesToRestore.Count > 0)
			{
				Logger.LogWarning($"復旧できなかったカテゴリ: {string.Join(", ", categoriesToRestore)}");
			}
			else
			{
				Logger.LogInfo("全てのカテゴリの復旧が完了しました");
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
