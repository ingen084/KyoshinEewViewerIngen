namespace KyoshinMonitorRecorder;

/// <summary>
/// ストレージサービスのインターフェース
/// </summary>
public interface IStorageService : IAsyncDisposable
{
	/// <summary>
	/// ストレージの種類を表す名前
	/// </summary>
	string StorageType { get; }

	/// <summary>
	/// ファイルが存在し、空でないかを確認する
	/// </summary>
	Task<bool> ExistsAndNotEmptyAsync(string key, CancellationToken cancellationToken = default);

	/// <summary>
	/// ファイルを保存する
	/// </summary>
	Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default);
}
