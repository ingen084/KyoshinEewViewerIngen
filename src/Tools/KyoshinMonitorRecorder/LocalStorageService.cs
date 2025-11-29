namespace KyoshinMonitorRecorder;

/// <summary>
/// ローカルファイルシステムへの保存を行うストレージサービス
/// </summary>
public class LocalStorageService : IStorageService
{
	private readonly string _baseDirectory;

	public LocalStorageService(string baseDirectory)
	{
		_baseDirectory = baseDirectory;
	}

	public string StorageType => "ローカル";

	public Task<bool> ExistsAndNotEmptyAsync(string key, CancellationToken cancellationToken = default)
	{
		var filePath = GetFullPath(key);
		if (!File.Exists(filePath))
			return Task.FromResult(false);

		var fileInfo = new FileInfo(filePath);
		return Task.FromResult(fileInfo.Length > 0);
	}

	public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default)
	{
		var filePath = GetFullPath(key);

		// ディレクトリを作成
		var directory = Path.GetDirectoryName(filePath);
		if (directory != null && !Directory.Exists(directory))
			Directory.CreateDirectory(directory);

		await using var fileStream = File.Open(filePath, FileMode.Create);
		await content.CopyToAsync(fileStream, cancellationToken);
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private string GetFullPath(string key)
		=> Path.Combine(_baseDirectory, key.Replace('/', Path.DirectorySeparatorChar));
}
