using System;
using System.IO;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers;

public abstract class TelegramPublisher
{
	/// <summary>
	/// 過去の情報を受信した
	/// 苦心した際は今までのキャッシュをすべて削除する必要がある
	/// </summary>
	public event Action<TelegramPublisher, string, InformationCategory, Telegram[]>? HistoryTelegramArrived;
	protected void OnHistoryTelegramArrived(string name, InformationCategory category, Telegram[] telegerams)
		=> HistoryTelegramArrived?.Invoke(this, name, category, telegerams);

	/// <summary>
	/// 情報受信
	/// </summary>
	public event Action<TelegramPublisher, InformationCategory, Telegram>? TelegramArrived;
	protected void OnTelegramArrived(InformationCategory category, Telegram info)
		=> TelegramArrived?.Invoke(this, category, info);

	/// <summary>
	/// 情報が失効した
	/// </summary>
	public event Action<TelegramPublisher, InformationCategory[], bool>? Failed;
	protected void OnFailed(InformationCategory[] category, bool isRestorable)
		=> Failed?.Invoke(this, category, isRestorable);

	/// <summary>
	/// サポートする情報カテゴリが変更された
	/// 変化がない可能性もある
	/// </summary>
	public event Action<TelegramPublisher>? InformationCategoryUpdated;
	protected void OnInformationCategoryUpdated()
		=> InformationCategoryUpdated?.Invoke(this);

	/// <summary>
	/// 初期化する
	/// </summary>
	/// <returns></returns>
	public abstract Task InitalizeAsync();

	/// <summary>
	/// サポートする情報カテゴリを取得する
	/// 初期化後に取得しなければならない
	/// </summary>
	public abstract Task<InformationCategory[]> GetSupportedCategoriesAsync();

	/// <summary>
	/// 購読を開始する
	/// </summary>
	/// <param name="category">購読するカテゴリ</param>
	public abstract void Start(InformationCategory[] categories);

	/// <summary>
	/// 購読を終了する
	/// </summary>
	public abstract void Stop(InformationCategory[] categories);
}

public class Telegram
{
	public Telegram(string key, string title, string rawId, DateTime arrivalTime, Func<Task<Stream>> getBodyFunc, Action? cleanupFunc)
	{
		Key = key ?? throw new ArgumentNullException(nameof(key));
		Title = title ?? throw new ArgumentNullException(nameof(title));
		RawId = rawId;
		ArrivalTime = arrivalTime;
		GetBodyFunc = getBodyFunc ?? throw new ArgumentNullException(nameof(getBodyFunc));
		CleanupFunc = cleanupFunc;
	}

	public string Key { get; }
	public string Title { get; }
	/// <summary>
	/// 生の電文ID<br/>
	/// VXSE のような文字列を <b>含む可能性がある</b>
	/// </summary>
	public string RawId { get; }
	public DateTime ArrivalTime { get; }
	private Func<Task<Stream>> GetBodyFunc { get; }
	private Action? CleanupFunc { get; }
	public Task<Stream> GetBodyAsync() => GetBodyFunc();
	public void Cleanup() => CleanupFunc?.Invoke();
}
