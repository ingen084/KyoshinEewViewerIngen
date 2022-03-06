using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;
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
		{ InformationCategory.Eew, new() },
		{ InformationCategory.Typhoon, new() },
	};
	private Dictionary<InformationCategory, TelegramPublisher?> UsingPublisher { get; } = new();

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
		var remainCategories = Subscribers.Keys.ToList();
		foreach (var publisher in Publishers)
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

	private void OnFailed(TelegramPublisher sender, InformationCategory[] categories, bool isRestorable)
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
				s.Failed();
			fallTargetCategories.Add(category);
		}

		// リストア可能もしくは対象が存在しなければ何もしない
		if (isRestorable || !fallTargetCategories.Any())
			return;

		// リストア不可の場合、次に優先度の高いプロバイダに切り替える
		var i = Publishers.IndexOf(sender);
		// フォールバック先が存在しない
		if (i >= (Publishers.Count - 1))
			return;

		var nextPublisher =  Publishers[i + 1];

		// 取得開始
		foreach (var category in fallTargetCategories)
			UsingPublisher[category] = nextPublisher;
		nextPublisher.Start(fallTargetCategories.ToArray());
	}

	private async void OnInformationCategoryUpdated(TelegramPublisher sender)
	{
		var stops = new Dictionary<TelegramPublisher, List<InformationCategory>>();

		// 再計算する
		var remainCategories = Subscribers.Keys.ToList();
		foreach (var publisher in Publishers)
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

		// 停止させる
		foreach(var s in stops)
			s.Key.Stop(s.Value.ToArray());
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
		Action failed)
	{
		if (Started)
			throw new InvalidOperationException("開始後の購読開始はできません。");
		if (!Subscribers.TryGetValue(category, out var subscribers))
			return;
		var subscriver = new Subscriber(sourceSwitched, arrived, failed);
		subscribers.Add(subscriver);
	}

	private sealed record Subscriber(Action<string, IEnumerable<Telegram>> SourceSwitched, Action<Telegram> Arrived, Action Failed);
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
	/// 緊急地震速報
	/// </summary>
	Eew,
	/// <summary>
	/// 台風情報
	/// </summary>
	Typhoon,
}
