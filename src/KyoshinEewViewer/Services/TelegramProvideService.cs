using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;
using Microsoft.Extensions.Logging;
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
	private List<TelegramPublisher> Publishers { get; } = new();
	private Dictionary<InformationCategory, List<Subscriber>> Subscribers { get; } = new()
	{
		{ InformationCategory.Earthquake, new() },
		{ InformationCategory.EewForecast, new() },
		{ InformationCategory.EewWarning, new() },
		{ InformationCategory.Tsunami, new() },
		{ InformationCategory.Typhoon, new() },
	};
	private Dictionary<InformationCategory, TelegramPublisher?> UsingPublisher { get; } = new();

	private ILogger Logger { get; } = LoggingService.CreateLogger<TelegramProvideService>();

	private bool Started { get; set; } = false;
	/// <summary>
	/// 開始する
	/// </summary>
	public async Task StartAsync()
	{
		if (Started)
			throw new InvalidOperationException("すでに開始しています");
		Started = true;

		var dmdata = new DmdataTelegramPublisher();
		dmdata.HistoryTelegramArrived += OnHistoryTelegramArrived;
		dmdata.TelegramArrived += OnTelegramArrived;
		dmdata.Failed += OnFailed;
		dmdata.InformationCategoryUpdated += OnInformationCategoryUpdated;
		await dmdata.InitalizeAsync();
		Publishers.Add(dmdata);

		var jmaXml = new JmaXmlTelegramPublisher();
		jmaXml.HistoryTelegramArrived += OnHistoryTelegramArrived;
		jmaXml.TelegramArrived += OnTelegramArrived;
		jmaXml.Failed += OnFailed;
		jmaXml.InformationCategoryUpdated += OnInformationCategoryUpdated;
		await jmaXml.InitalizeAsync();
		Publishers.Add(jmaXml);

		// 割り当てられていないカテゴリたち
		var remainCategories = Subscribers.Where(s => s.Value.Any()).Select(s => s.Key).ToList();
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
				Logger.LogError(ex, "電文プロバイダ {name} の初期化中に例外が発生しました。", publisher.GetType().Name);
			}
		}
	}

	private void OnHistoryTelegramArrived(TelegramPublisher sender, string name, InformationCategory category, Telegram[] telegrams)
	{
		// 現在利用中のプロバイダからでなければ無視
		if (UsingPublisher[category] != sender)
			return;

		foreach (var s in Subscribers[category])
			s.SourceSwitched(name, telegrams);
	}
	private void OnTelegramArrived(TelegramPublisher sender, InformationCategory category, Telegram telegram)
	{
		// 現在利用中のプロバイダからでなければ無視
		if (UsingPublisher[category] != sender)
			return;

		foreach (var s in Subscribers[category])
			s.Arrived(telegram);
	}

	private async void OnFailed(TelegramPublisher sender, InformationCategory[] categories, bool isRestorable)
	{
		// 使用中のもののみフォールバックできるようにする
		var fallTargetCategories = new List<InformationCategory>();
		foreach (var category in categories)
		{
			// 現在利用中のプロバイダからでなければ無視
			if (UsingPublisher[category] != sender)
				continue;

			// Failed通知を送信、フロントは操作不能になる
			foreach (var s in Subscribers[category])
				s.Failed(false);
			fallTargetCategories.Add(category);
		}

		// リストア可能もしくは対象が存在しなければ何もしない
		if (isRestorable || !fallTargetCategories.Any())
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
						s.Failed(true);
					break;
				}
				nextPublisher = Publishers[i + 1];

				if (!(await nextPublisher.GetSupportedCategoriesAsync()).Contains(category))
					continue;
				if (!matchedPublishers.ContainsKey(nextPublisher))
					matchedPublishers.Add(nextPublisher, new());
				matchedPublishers[nextPublisher].Add(category);
				break;
			}
		}
		foreach (var p in matchedPublishers)
		{
			foreach (var c in p.Value)
				UsingPublisher[c] = p.Key;
			p.Key.Start(p.Value.ToArray());
		}
	}

	private async void OnInformationCategoryUpdated(TelegramPublisher sender)
	{
		var stops = new Dictionary<TelegramPublisher, List<InformationCategory>>();

		// 再計算する
		var remainCategories = Subscribers.Where(s => s.Value.Any()).Select(s => s.Key).ToList();
		foreach (var publisher in Publishers)
		{
			try
			{
				var supported = await publisher.GetSupportedCategoriesAsync();
				var matched = supported.Where(s => remainCategories.Contains(s));
				if (!matched.Any())
					continue;

				// 追加項目のみ 念の為優先度を確認しておく
				var added = matched.Where(m => UsingPublisher[m] != publisher).ToArray();
				// 割当
				foreach (var mc in added)
				{
					if (UsingPublisher[mc] != null)
					{
						if (!stops.ContainsKey(UsingPublisher[mc]!))
							stops.Add(UsingPublisher[mc]!, new List<InformationCategory>() { mc });
						else
							stops[UsingPublisher[mc]!].Add(mc);
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
				Logger.LogError(ex, "電文プロバイダ {name} へのフォールバック中に例外が発生しました。", publisher.GetType().Name);
			}
		}

		// 停止させる
		foreach (var s in stops)
			s.Key.Stop(s.Value.ToArray());
	}

	public async Task RestoreAsync()
	{
		// 割り当てられていないカテゴリたち
		var unassignedCategory = Subscribers.Where(s => s.Value.Any() && (!UsingPublisher.TryGetValue(s.Key, out var p) || p == null)).Select(s => s.Key).ToList();
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
				Logger.LogError(ex, "電文プロバイダ {name} の初期化中に例外が発生しました。", publisher.GetType().Name);
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
		Action<string, IEnumerable<Telegram>> sourceSwitched,
		Action<Telegram> arrived,
		Action<bool> failed)
	{
		if (Started)
			throw new InvalidOperationException("開始後の購読開始はできません。");
		if (!Subscribers.TryGetValue(category, out var subscribers))
			return;
		var subscriver = new Subscriber(sourceSwitched, arrived, failed);
		subscribers.Add(subscriver);
	}

	private sealed record Subscriber(Action<string, IEnumerable<Telegram>> SourceSwitched, Action<Telegram> Arrived, Action<bool> Failed);
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
