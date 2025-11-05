using Avalonia.Platform;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

/// <summary>
/// 観測点情報の自動更新を管理するシングルトンサービス
/// </summary>
public class ObservationPointsUpdateService : ReactiveObject
{
	private const string GithubReleasesUrl = "https://api.github.com/repos/ingen084/kyoshin-monitor-observation-points/releases";
	private const string AssetFileName = "intensity-points-v2.kmop";
	private const string BuiltinAssetPath = "avares://KyoshinEewViewer/Assets/observation-points.kmop";

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private HttpClient Client { get; } = new();

	// キャッシュされた観測点データ
	private ObservationPointV2[]? _cachedObservationPoints;
	private ObservationPointsFileHeader? _currentHeader;

	private bool _isUpdating;
	public bool IsUpdating
	{
		get => _isUpdating;
		set => this.RaiseAndSetIfChanged(ref _isUpdating, value);
	}

	private string _updateStatus = "未更新";
	public string UpdateStatus
	{
		get => _updateStatus;
		set => this.RaiseAndSetIfChanged(ref _updateStatus, value);
	}

	public ObservationPointsUpdateService(ILogManager logManager, KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<ObservationPointsUpdateService>();

		Logger = logManager.GetLogger<ObservationPointsUpdateService>();
		Config = config;
		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEVi;" + Utils.Version);
	}

	/// <summary>
	/// キャッシュディレクトリのパスを取得
	/// </summary>
	private string GetCacheDirectory() => PlatformDirectories.ApplicationData;

	/// <summary>
	/// キャッシュファイルのパスを取得
	/// </summary>
	private string GetCacheFilePath() => Path.Combine(GetCacheDirectory(), "observation-points.kmop");

	/// <summary>
	/// 現在使用中の観測点データヘッダを取得
	/// </summary>
	public ObservationPointsFileHeader? GetCurrentHeader()
	{
		return _currentHeader;
	}

	/// <summary>
	/// 現在使用中の観測点数を取得
	/// </summary>
	public int GetObservationPointsCount()
	{
		return _cachedObservationPoints?.Length ?? 0;
	}

	/// <summary>
	/// 観測点データを取得（自動更新が有効な場合は更新チェックも実行）
	/// </summary>
	public async Task<ObservationPointV2[]> GetObservationPointsAsync()
	{
		// キャッシュがあればそれを返す
		if (_cachedObservationPoints != null)
			return _cachedObservationPoints;

		// 自動更新が有効な場合、更新チェックを実行
		if (Config.KyoshinMonitor.AutoUpdateObservationPoints)
		{
			try
			{
				await CheckAndUpdateAsync();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "観測点情報の更新チェックに失敗しました");
			}
		}

		// 最新のデータを読み込み
		_cachedObservationPoints = await LoadLatestDataAsync();
		return _cachedObservationPoints;
	}

	/// <summary>
	/// バージョンチェックと自動更新を実行
	/// </summary>
	public async Task CheckAndUpdateAsync()
	{
		if (IsUpdating)
			return;

		IsUpdating = true;
		UpdateStatus = "更新チェック中";

		try
		{
			Logger.LogDebug("観測点情報の更新チェックを開始します");

			// ローカルデータの作成時刻を取得
			var localPackedAt = await GetLocalDataPackedAtAsync();

			// GitHub Releasesから最新情報を取得
			var releases = await GitHubRelease.GetReleasesAsync(Client, GithubReleasesUrl);
			var latestRelease = releases
				.Where(r => !r.Draft)
				.OrderByDescending(r => r.CreatedAt)
				.FirstOrDefault();

			if (latestRelease == null)
			{
				Logger.LogDebug("利用可能な観測点情報のリリースが見つかりませんでした");
				UpdateStatus = "更新なし";
				return;
			}

			// 対象アセットを検索
			var asset = latestRelease.Assets?.FirstOrDefault(a => a.Name == AssetFileName);
			if (asset == null)
			{
				Logger.LogDebug($"リリース内に {AssetFileName} が見つかりませんでした");
				UpdateStatus = "更新なし";
				return;
			}

			// バージョン比較（created_atが1分以上後の場合は新しいと判定）
			if (localPackedAt.HasValue && latestRelease.CreatedAt <= localPackedAt.Value.AddMinutes(1))
			{
				Logger.LogDebug($"観測点情報は最新です (ローカル: {localPackedAt}, GitHub: {latestRelease.CreatedAt})");
				UpdateStatus = "最新";
				return;
			}

			Logger.LogInfo($"新しい観測点情報が見つかりました (ローカル: {localPackedAt}, GitHub: {latestRelease.CreatedAt})");
			UpdateStatus = "ダウンロード中";

			// ダウンロードと保存
			await DownloadAndSaveAsync(asset.BrowserDownloadUrl);

			// キャッシュをクリアして次回読み込み時に新しいデータを使用
			_cachedObservationPoints = null;

			UpdateStatus = "更新完了";
			Logger.LogInfo("観測点情報の更新が完了しました");
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "観測点情報の更新チェック中にエラーが発生しました");
			UpdateStatus = "更新失敗";
		}
		finally
		{
			IsUpdating = false;
		}
	}

	/// <summary>
	/// 手動更新を実行
	/// </summary>
	public async Task<bool> ManualUpdateAsync()
	{
		try
		{
			await CheckAndUpdateAsync();
			return UpdateStatus == "更新完了";
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "手動更新に失敗しました");
			return false;
		}
	}

	/// <summary>
	/// ローカルデータ（キャッシュまたは内蔵）の作成時刻を取得
	/// </summary>
	private async Task<DateTime?> GetLocalDataPackedAtAsync()
	{
		// キャッシュファイルを優先
		var cachePath = GetCacheFilePath();
		if (File.Exists(cachePath))
		{
			try
			{
				using var stream = File.OpenRead(cachePath);
				using var reader = new ObservationPointsFileReader(stream);
				var header = await reader.ReadHeader();
				return header.PackedAt;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "キャッシュファイルの読み込みに失敗しました");
			}
		}

		// 内蔵データを確認
		try
		{
			using var stream = AssetLoader.Open(new Uri(BuiltinAssetPath, UriKind.Absolute));
			using var reader = new ObservationPointsFileReader(stream);
			var header = await reader.ReadHeader();
			return header.PackedAt;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "内蔵データの読み込みに失敗しました");
			return null;
		}
	}

	/// <summary>
	/// 最新のデータを読み込み
	/// </summary>
	private async Task<ObservationPointV2[]> LoadLatestDataAsync()
	{
		DateTime? cachePackedAt = null;
		DateTime? builtinPackedAt = null;

		// キャッシュファイルの作成時刻を取得
		var cachePath = GetCacheFilePath();
		if (File.Exists(cachePath))
		{
			try
			{
				using var stream = File.OpenRead(cachePath);
				using var reader = new ObservationPointsFileReader(stream);
				var header = await reader.ReadHeader();
				cachePackedAt = header.PackedAt;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "キャッシュファイルのヘッダ読み込みに失敗しました");
			}
		}

		// 内蔵データの作成時刻を取得
		try
		{
			using var stream = AssetLoader.Open(new Uri(BuiltinAssetPath, UriKind.Absolute));
			using var reader = new ObservationPointsFileReader(stream);
			var header = await reader.ReadHeader();
			builtinPackedAt = header.PackedAt;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "内蔵データのヘッダ読み込みに失敗しました");
		}

		// より新しいデータを選択
		bool useCached = cachePackedAt.HasValue &&
		                 (!builtinPackedAt.HasValue || cachePackedAt.Value > builtinPackedAt.Value);

		if (useCached)
		{
			Logger.LogInfo($"キャッシュデータを使用します (作成日時: {cachePackedAt})");
			return await LoadDataFromFileAsync(cachePath);
		}
		else
		{
			Logger.LogInfo($"内蔵データを使用します (作成日時: {builtinPackedAt})");
			return await LoadDataFromBuiltinAsync();
		}
	}

	/// <summary>
	/// ファイルからデータを読み込み
	/// </summary>
	private async Task<ObservationPointV2[]> LoadDataFromFileAsync(string path)
	{
		using var stream = File.OpenRead(path);
		using var reader = new ObservationPointsFileReader(stream);
		_currentHeader = await reader.ReadHeader();
		var data = await reader.ReadData(_currentHeader.CompressionMode);
		return data;
	}

	/// <summary>
	/// 内蔵アセットからデータを読み込み
	/// </summary>
	private async Task<ObservationPointV2[]> LoadDataFromBuiltinAsync()
	{
		using var stream = AssetLoader.Open(new Uri(BuiltinAssetPath, UriKind.Absolute));
		using var reader = new ObservationPointsFileReader(stream);
		_currentHeader = await reader.ReadHeader();
		var data = await reader.ReadData(_currentHeader.CompressionMode);
		return data;
	}

	/// <summary>
	/// データをダウンロードして保存
	/// </summary>
	private async Task DownloadAndSaveAsync(string url)
	{
		var tempPath = Path.GetTempFileName();

		try
		{
			// ダウンロード
			Logger.LogDebug($"観測点情報をダウンロードします: {url}");
			using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();

				await using var stream = await response.Content.ReadAsStreamAsync();
				await using var fileStream = File.Create(tempPath);
				await stream.CopyToAsync(fileStream);
			}

			// 検証
			Logger.LogDebug("ダウンロードしたファイルを検証します");
			using (var stream = File.OpenRead(tempPath))
			{
				using var reader = new ObservationPointsFileReader(stream);
				var header = await reader.ReadHeader();
				var data = await reader.ReadData(header.CompressionMode);

				if (data == null || data.Length == 0)
					throw new Exception("観測点データが空です");

				Logger.LogInfo($"観測点情報の検証が完了しました (観測点数: {data.Length}, 作成日時: {header.PackedAt})");
			}

			// キャッシュディレクトリを作成
			var cacheDir = GetCacheDirectory();
			if (!Directory.Exists(cacheDir))
				Directory.CreateDirectory(cacheDir);

			// ファイルを移動
			var cachePath = GetCacheFilePath();
			File.Copy(tempPath, cachePath, true);

			Logger.LogDebug($"観測点情報をキャッシュに保存しました: {cachePath}");
		}
		finally
		{
			// 一時ファイルを削除
			if (File.Exists(tempPath))
			{
				try
				{
					File.Delete(tempPath);
				}
				catch
				{
					// 削除失敗は無視
				}
			}
		}
	}
}
