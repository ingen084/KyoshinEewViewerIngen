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
	private DmdataTelegramPublisher Dmdata { get; }
	private JmaXmlTelegramPublisher Jma { get; }

	public TelegramProvideService(ILogManager logManager, DmdataTelegramPublisher dmdata, JmaXmlTelegramPublisher jma)
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
				if (!matched.Any())
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
			var stops = new Dictionary<TelegramPublisher, List<InformationCategory>>();

			// 再計算する
			var remainCategories = Subscribers.Where(s => s.Value.Count != 0).Select(s => s.Key).ToList();
			foreach (var publisher in Publishers)
			{
				try
				{
					var supported = await publisher.GetSupportedCategoriesAsync();
					var matched = supported.Where(s => remainCategories.Contains(s));
					if (!matched.Any())
						continue;

					// 追加項目のみ 念の為優先度を確認しておく
					var added = matched.Where(m => !UsingPublisher.TryGetValue(m, out var up) || up != sender).ToArray();
					// 割当
					foreach (var mc in added)
					{
						if (UsingPublisher.TryGetValue(mc, out var up) && up != null)
						{
							if (!stops.ContainsKey(up))
								stops.Add(up, [mc]);
							else
								stops[up].Add(mc);
						}
						UsingPublisher[mc] = publisher;
					}
					// 開始
					if (added.Any())
						publisher.Start(added);

					remainCategories.RemoveAll(c => supported.Contains(c));
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, $"電文プロバイダ {publisher.GetType().Name} へのフォールバック中に例外が発生しました。");
				}
			}

			// 停止させる
			foreach (var s in stops)
				s.Key.Stop(s.Value.ToArray());
		});

	public async Task RestoreAsync()
	{
		// 割り当てられていないカテゴリたち
		var unassignedCategory = Subscribers.Where(s => s.Value.Count != 0 && (!UsingPublisher.TryGetValue(s.Key, out var p) || p == null)).Select(s => s.Key).ToList();
		foreach (var publisher in Publishers)
		{
			try
			{
				var supported = await publisher.GetSupportedCategoriesAsync();
				var matched = supported.Where(s => unassignedCategory.Contains(s)).ToArray();
				if (!matched.Any())
					continue;

				// 割当
				foreach (var mc in matched)
					UsingPublisher[mc] = publisher;
				// 開始
				publisher.Start(matched);

				unassignedCategory.RemoveAll(c => supported.Contains(c));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, $"電文プロバイダ {publisher.GetType().Name} の初期化中に例外が発生しました。");
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
		Func<string, IEnumerable<Telegram>, Task> sourceSwitched,
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

	private sealed record Subscriber(Func<string, IEnumerable<Telegram>, Task> SourceSwitched, Func<Telegram, Task> Arrived, Action<(bool isAllFailed, bool isRestorable)> Failed);
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
