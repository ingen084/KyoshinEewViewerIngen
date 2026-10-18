using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;

using System;
using System.Collections.Concurrent;
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
	/// <summary>
	/// カテゴリごとに使用中のPublisher<br/>
	/// 割り当ての変更は <see cref="_assignmentLock"/> を取得して行い、Publisher の購読状態と一致させる
	/// </summary>
	private ConcurrentDictionary<InformationCategory, TelegramPublisher> UsingPublisher { get; } = [];

	/// <summary>
	/// 割り当ての変更 (失敗時の切り替え・優先度による復帰・復旧) を順番に処理するためのロック
	/// </summary>
	private readonly SemaphoreSlim _assignmentLock = new(1, 1);

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

		await _assignmentLock.WaitAsync();
		try
		{
			var remainCategories = await AssignAsync(Subscribers.Where(s => s.Value.Count != 0).Select(s => s.Key).ToList());
			if (remainCategories.Count == 0)
				return;

			Logger.LogWarning("以下のカテゴリに対応するPublisherが見つかりませんでした: {RemainCategories}", string.Join(", ", remainCategories));
			NotifyFailed(remainCategories, (isAllFailed: true, isRestorable: false));
		}
		finally
		{
			_assignmentLock.Release();
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
	{
		// リストア可能であれば割り当ては変えずにプロバイダ自身の復旧を待つ
		// 同じプロバイダから続けて届く履歴の通知と順序が入れ替わらないよう、すぐに通知する
		if (isRestorable)
		{
			NotifyFailed(categories.Where(c => UsingPublisher.TryGetValue(c, out var up) && up == sender).Distinct(), (isAllFailed: false, isRestorable: true));
			return;
		}

		_ = Task.Run(async () =>
		{
			await _assignmentLock.WaitAsync();
			try
			{
				// 現在利用中のプロバイダからでなければ無視
				var failedCategories = categories.Where(c => UsingPublisher.TryGetValue(c, out var up) && up == sender).Distinct().ToList();
				if (failedCategories.Count == 0)
					return;

				// Failed通知を送信、フロントは操作不能になる
				NotifyFailed(failedCategories, (isAllFailed: false, isRestorable: false));

				// 失敗したプロバイダの購読を止めておき、復旧後に開始し直したときに履歴を受け取れるようにする
				foreach (var category in failedCategories)
					UsingPublisher.TryRemove(category, out _);
				sender.Stop(failedCategories.ToArray());

				// 優先度の低いプロバイダに切り替える
				var remainCategories = await AssignAsync(failedCategories, Publishers.IndexOf(sender) + 1);
				if (remainCategories.Count == 0)
					return;

				// フォールバック先が存在しない
				Logger.LogWarning("以下のカテゴリは受信できる情報源がありません: {RemainCategories}", string.Join(", ", remainCategories));
				NotifyFailed(remainCategories, (isAllFailed: true, isRestorable: false));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "情報ソース切り替え中に例外が発生しました");
			}
			finally
			{
				_assignmentLock.Release();
			}
		});
	}

	private void OnInformationCategoryUpdated(TelegramPublisher sender)
		=> Task.Run(async () =>
		{
			await _assignmentLock.WaitAsync();
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
							if (UsingPublisher.TryGetValue(category, out var currentPublisher))
							{
								// 現在のPublisherより優先度が高い場合（Publishersリストの先頭に近い）
								if (Publishers.IndexOf(publisher) < Publishers.IndexOf(currentPublisher))
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
							if (UsingPublisher.TryGetValue(category, out var oldPublisher))
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
			finally
			{
				_assignmentLock.Release();
			}
		});

	/// <summary>
	/// 受信できていないカテゴリの受信を再試行する<br/>
	/// 復旧できなかったカテゴリは、購読者へ再度失敗を通知する
	/// </summary>
	public async Task RestoreAsync()
	{
		await _assignmentLock.WaitAsync();
		try
		{
			Logger.LogInformation("電文プロバイダの復旧処理を開始します");

			// 1. 割り当てられていないカテゴリ
			var categoriesToRestore = Subscribers
				.Where(s => s.Value.Count != 0 && !UsingPublisher.ContainsKey(s.Key))
				.Select(s => s.Key)
				.ToList();

			// 2. 現在のPublisherがサポートしなくなったカテゴリ（障害状態など）
			foreach (var group in UsingPublisher.GroupBy(u => u.Value, u => u.Key).ToArray())
			{
				InformationCategory[] supported;
				try
				{
					supported = await group.Key.GetSupportedCategoriesAsync();
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "プロバイダ {Name} のサポートカテゴリ取得に失敗したため復旧対象とします", group.Key.GetType().Name);
					supported = [];
				}
				var unsupported = group.Where(c => !supported.Contains(c)).ToArray();
				if (unsupported.Length == 0)
					continue;

				Logger.LogInformation("カテゴリ {Categories} は現在のプロバイダ {Name} でサポートされていないため復旧対象とします", string.Join(", ", unsupported), group.Key.GetType().Name);
				foreach (var category in unsupported)
					UsingPublisher.TryRemove(category, out _);
				group.Key.Stop(unsupported);
				categoriesToRestore.AddRange(unsupported);
			}

			if (categoriesToRestore.Count == 0)
			{
				Logger.LogInformation("復旧対象のカテゴリは見つかりませんでした");
				return;
			}

			Logger.LogInformation("復旧対象カテゴリ: {CategoriesToRestore}", string.Join(", ", categoriesToRestore));

			var remainCategories = await AssignAsync(categoriesToRestore);
			if (remainCategories.Count == 0)
			{
				Logger.LogInformation("全てのカテゴリの復旧が完了しました");
				return;
			}

			Logger.LogWarning("復旧できなかったカテゴリ: {CategoriesToRestore}", string.Join(", ", remainCategories));
			NotifyFailed(remainCategories, (isAllFailed: true, isRestorable: false));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "復旧処理中に予期しない例外が発生しました");
		}
		finally
		{
			_assignmentLock.Release();
		}
	}

	/// <summary>
	/// 優先度順にカテゴリをPublisherへ割り当てて受信を開始する<br/>
	/// <see cref="_assignmentLock"/> を取得した状態で呼び出す
	/// </summary>
	/// <param name="startIndex">割り当てを試みる最初のPublisherの位置</param>
	/// <returns>割り当てられなかったカテゴリ</returns>
	private async Task<List<InformationCategory>> AssignAsync(List<InformationCategory> categories, int startIndex = 0)
	{
		var remainCategories = categories.ToList();
		foreach (var publisher in Publishers.Skip(startIndex))
		{
			if (remainCategories.Count == 0)
				break;
			try
			{
				var supported = await publisher.GetSupportedCategoriesAsync();
				var matched = remainCategories.Where(supported.Contains).ToArray();
				if (matched.Length == 0)
					continue;

				Logger.LogInformation("{Name} で {Matched} を受信します", publisher.GetType().Name, string.Join(", ", matched));
				foreach (var category in matched)
					UsingPublisher[category] = publisher;
				publisher.Start(matched);

				remainCategories.RemoveAll(matched.Contains);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "電文プロバイダ {Name} の開始中に例外が発生しました。", publisher.GetType().Name);
			}
		}
		return remainCategories;
	}

	private void NotifyFailed(IEnumerable<InformationCategory> categories, (bool isAllFailed, bool isRestorable) state)
	{
		foreach (var category in categories)
		{
			foreach (var subscriber in Subscribers[category])
			{
				try
				{
					subscriber.Failed(state);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "カテゴリ {Category} のサブスクライバーへの失敗報告中に例外が発生しました", category);
				}
			}
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
		_assignmentLock?.Dispose();

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
