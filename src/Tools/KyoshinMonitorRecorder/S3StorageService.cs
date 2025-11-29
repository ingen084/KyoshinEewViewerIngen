using Amazon.S3;
using Amazon.S3.Model;

namespace KyoshinMonitorRecorder;

/// <summary>
/// S3互換ストレージへの保存を行うストレージサービス
/// </summary>
public class S3StorageService : IStorageService
{
	private readonly AmazonS3Client _s3Client;
	private readonly string _bucketName;
	private readonly string? _prefix;

	public S3StorageService(string endpoint, string accessKey, string secretKey, string bucketName, string? prefix = null)
	{
		var config = new AmazonS3Config
		{
			ServiceURL = endpoint,
			ForcePathStyle = true,
		};
		_s3Client = new AmazonS3Client(accessKey, secretKey, config);
		_bucketName = bucketName;
		_prefix = prefix?.TrimEnd('/');
	}

	public string StorageType => "S3";

	public async Task<bool> ExistsAndNotEmptyAsync(string key, CancellationToken cancellationToken = default)
	{
		try
		{
			var fullKey = GetFullKey(key);
			var response = await _s3Client.GetObjectMetadataAsync(_bucketName, fullKey, cancellationToken);
			return response.ContentLength > 0;
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default)
	{
		var fullKey = GetFullKey(key);
		var request = new PutObjectRequest
		{
			BucketName = _bucketName,
			Key = fullKey,
			InputStream = content,
			ContentType = "application/octet-stream",
		};

		await _s3Client.PutObjectAsync(request, cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		_s3Client.Dispose();
		await Task.CompletedTask;
	}

	private string GetFullKey(string key)
		=> string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}/{key}";
}
