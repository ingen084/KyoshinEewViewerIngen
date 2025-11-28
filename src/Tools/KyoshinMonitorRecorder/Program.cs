using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinMonitorLib.UrlGenerator;
using System.Net;

namespace KyoshinMonitorRecorder;

/// <summary>
/// 強震モニタのリアルタイムデータを録画するツール
/// </summary>
public static class Program
{
	/// <summary>
	/// 保存先ディレクトリ（相対パス）
	/// </summary>
	private const string OutputDirectory = "recordings";

	/// <summary>
	/// ファイル分割間隔（分）
	/// </summary>
	private const int SplitIntervalMinutes = 10;

	/// <summary>
	/// 欠損補完対象の時間範囲（時間）
	/// </summary>
	private const int BackfillHours = 3;

	public static async Task Main(string[] args)
	{
		Console.WriteLine("強震モニタ録画ツール - KyoshinMonitorRecorder");
		Console.WriteLine($"保存先: {Path.GetFullPath(OutputDirectory)}");
		Console.WriteLine($"分割間隔: {SplitIntervalMinutes}分");
		Console.WriteLine($"欠損補完範囲: 直近{BackfillHours}時間");
		Console.WriteLine();

		if (!Directory.Exists(OutputDirectory))
		{
			Console.WriteLine("保存先フォルダを作成します...");
			Directory.CreateDirectory(OutputDirectory);
		}

		// レガシーファイルを日付フォルダに移行
		MigrateLegacyFiles();

		var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (sender, e) =>
		{
			e.Cancel = true;
			cts.Cancel();
			Console.WriteLine("\n停止要求を受け付けました。現在のデータを保存せず終了します...");
		};

		// 統合データ取得ループを開始
		await StartRecordingLoopAsync(cts.Token);
	}

	/// <summary>
	/// レガシー形式のファイル（yyyyMMdd_HHmm.eqrp）を日付フォルダに移行する
	/// </summary>
	private static void MigrateLegacyFiles()
	{
		var legacyFiles = Directory.GetFiles(OutputDirectory, "????????_????.eqrp");
		if (legacyFiles.Length == 0)
			return;

		Console.WriteLine($"レガシーファイルを移行中: {legacyFiles.Length}件");

		var migratedCount = 0;
		var failedCount = 0;

		foreach (var filePath in legacyFiles)
		{
			var fileName = Path.GetFileNameWithoutExtension(filePath);

			// yyyyMMdd_HHmm 形式をパース
			if (fileName.Length != 13 || fileName[8] != '_')
			{
				failedCount++;
				continue;
			}

			var datePart = fileName[..8];
			var timePart = fileName[9..];

			// 日付フォルダを作成
			var targetDir = Path.Combine(OutputDirectory, datePart);
			if (!Directory.Exists(targetDir))
				Directory.CreateDirectory(targetDir);

			// 新しいパスに移動
			var newFilePath = Path.Combine(targetDir, $"{timePart}.eqrp");
			try
			{
				if (File.Exists(newFilePath))
				{
					// 既存ファイルがある場合は上書きしない
					Console.WriteLine($"  スキップ（既存）: {fileName}.eqrp");
					failedCount++;
					continue;
				}

				File.Move(filePath, newFilePath);
				migratedCount++;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"  移行失敗: {fileName}.eqrp - {ex.Message}");
				failedCount++;
			}
		}

		Console.WriteLine($"移行完了: {migratedCount}件" + (failedCount > 0 ? $", スキップ: {failedCount}件" : ""));
		Console.WriteLine();
	}

	/// <summary>
	/// 統合データ取得ループ
	/// 欠損補完とリアルタイム録画を統一したループで処理する
	/// </summary>
	private static async Task StartRecordingLoopAsync(CancellationToken cancellationToken)
	{
		Console.WriteLine("データ取得を開始します。");
		Console.WriteLine("停止するには Ctrl+C を押してください。\n");

		// 開始時刻を決定（欠損区間の最初から開始）
		var startTime = DetermineStartTime();
		Console.WriteLine($"開始時刻: {startTime:yyyy/MM/dd HH:mm:ss}");

		using var httpClient = CreateHttpClient();

		var currentIntervalData = new List<ReplayData>();
		var currentIntervalStart = RoundDownToInterval(startTime);
		var currentTargetTime = startTime;
		var totalReceived = 0;
		var totalSaved = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				// 取得可能な最大時刻（現在時刻の1分前）
				var maxFetchableTime = GetMaxFetchableTime();

				// 対象時刻が取得可能時刻を超えている場合は待機
				if (currentTargetTime > maxFetchableTime)
				{
					await Task.Delay(100, cancellationToken);
					continue;
				}

				// 区間が変わったら保存
				var targetIntervalStart = RoundDownToInterval(currentTargetTime);
				if (targetIntervalStart != currentIntervalStart && currentIntervalData.Count > 0)
				{
					// 既存ファイルがある場合はスキップ
					var existingFilePath = GetFilePath(currentIntervalStart);
					if (File.Exists(existingFilePath) && new FileInfo(existingFilePath).Length > 0)
					{
						Console.WriteLine($"\nスキップ（既存）: {GetFileName(currentIntervalStart)}");
					}
					else
					{
						await SaveFileAsync(currentIntervalStart, currentIntervalData);
						totalSaved += currentIntervalData.Count;
						Console.WriteLine($"\n保存完了: {GetFileName(currentIntervalStart)} ({currentIntervalData.Count}件)");
					}
					currentIntervalData.Clear();
				}
				currentIntervalStart = targetIntervalStart;

				// 現在の区間のファイルが既に存在する場合は次の区間までスキップ
				var currentFilePath = GetFilePath(currentIntervalStart);
				if (File.Exists(currentFilePath) && new FileInfo(currentFilePath).Length > 0)
				{
					var nextIntervalStart = currentIntervalStart.AddMinutes(SplitIntervalMinutes);
					Console.WriteLine($"\nスキップ（既存）: {GetFileName(currentIntervalStart)} → {nextIntervalStart:HH:mm} から再開");
					currentTargetTime = nextIntervalStart;
					currentIntervalStart = nextIntervalStart;
					continue;
				}

				try
				{
					var fetchStartTime = DateTime.Now;

					var (replayData, partialFailures) = await FetchKyoshinDataAsync(httpClient, currentTargetTime, cancellationToken);
					if (replayData != null)
					{
						currentIntervalData.AddRange(replayData);
						totalReceived++;
						if (partialFailures.Count > 0)
							Console.WriteLine($"\n{currentTargetTime:HH:mm:ss} 部分的な失敗: {string.Join(", ", partialFailures)}");
					}
					else
					{
						Console.WriteLine($"\n{currentTargetTime:HH:mm:ss} 完全に失敗: {string.Join(", ", partialFailures)}");
					}

					// 遅延状況を計算
					var lag = maxFetchableTime - currentTargetTime;
					var isCatchingUp = lag > TimeSpan.FromMinutes(1);

					// 進捗表示
					if (isCatchingUp)
						Console.Write($"\r補完中: {currentTargetTime:yyyy/MM/dd HH:mm:ss} | 遅延: {lag:hh\\:mm\\:ss} | 受信: {totalReceived}件 | 保存済: {totalSaved}件");
					else
						Console.Write($"\rリアルタイム: {currentTargetTime:yyyy/MM/dd HH:mm:ss} | 受信: {totalReceived}件 | 保存済: {totalSaved}件 | バッファ: {currentIntervalData.Count}件");

					currentTargetTime = currentTargetTime.AddSeconds(1);

					// 待機時間を決定（追いつくまでは高速、リアルタイムでは1秒間隔）
					var fetchDuration = DateTime.Now - fetchStartTime;
					int delay;
					if (isCatchingUp)
						delay = Math.Max(50, 300 - (int)fetchDuration.TotalMilliseconds);
					else
						delay = Math.Max(100, 1000 - (int)fetchDuration.TotalMilliseconds);

					if (delay > 0)
						await Task.Delay(delay, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"\n{currentTargetTime:yyyy/MM/dd HH:mm:ss} の取得に失敗: {ex.Message}");
					currentTargetTime = currentTargetTime.AddSeconds(1);
					await Task.Delay(1000, cancellationToken);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// 正常な終了
		}

		Console.WriteLine($"\n録画を終了しました。総受信: {totalReceived}件, 総保存: {totalSaved}件");
	}

	/// <summary>
	/// 開始時刻を決定する（欠損区間の最初から開始）
	/// </summary>
	private static DateTime DetermineStartTime()
	{
		var now = DateTime.Now;
		var backfillStart = now.AddHours(-BackfillHours);
		var maxFetchableTime = GetMaxFetchableTime();

		// 補完対象の時間範囲を10分単位で確認
		var currentStart = RoundDownToInterval(backfillStart);

		while (currentStart < now)
		{
			var filePath = GetFilePath(currentStart);

			// ファイルが存在しない、または空のファイルは欠損区間
			if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
				return currentStart;

			currentStart = currentStart.AddMinutes(SplitIntervalMinutes);
		}

		// 欠損がない場合は取得可能な最大時刻から開始
		return maxFetchableTime;
	}

	/// <summary>
	/// 取得可能な最大時刻を取得する（現在時刻の1分前、秒単位で切り捨て）
	/// </summary>
	private static DateTime GetMaxFetchableTime()
	{
		var time = DateTime.Now.AddMinutes(-1);
		return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);
	}

	/// <summary>
	/// 強震モニタのデータを取得する
	/// </summary>
	/// <returns>取得したデータと部分的な失敗情報</returns>
	private static async Task<(List<ReplayData>? Data, List<string> PartialFailures)> FetchKyoshinDataAsync(HttpClient httpClient, DateTime targetTime, CancellationToken cancellationToken)
	{
		var result = new List<ReplayData>();
		var partialFailures = new List<string>();

		// EEW JSON を取得
		var eewJsonUrl = WebApiUrlGenerator.Generate(WebApiUrlType.EewJson, targetTime);
		var eewJson = await FetchWithRetryAsync(httpClient, eewJsonUrl, cancellationToken);
		if (eewJson == null)
			partialFailures.Add("EEW JSON");

		// リアルタイム震度を取得
		var shindoUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, targetTime, RealtimeDataType.Shindo, false);
		var shindoData = await FetchWithRetryAsync(httpClient, shindoUrl, cancellationToken);
		if (shindoData == null)
			partialFailures.Add("震度画像");

		// 最大加速度を取得
		var pgaUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, targetTime, RealtimeDataType.Pga, false);
		var pgaData = await FetchWithRetryAsync(httpClient, pgaUrl, cancellationToken);
		if (pgaData == null)
			partialFailures.Add("加速度画像");

		if (eewJson == null && shindoData == null && pgaData == null)
			return (null, partialFailures);

		// EEW JSON を追加
		if (eewJson != null)
		{
			result.Add(new KyoshinMonitorEewJsonReplayData
			{
				Time = targetTime.AddSeconds(1),
				Json = System.Text.Encoding.UTF8.GetString(eewJson),
			});
		}

		// 画像を追加
		var images = new Dictionary<KyoshinMonitorImageReplayData.ImageType, byte[]>();
		if (shindoData != null)
			images.Add(KyoshinMonitorImageReplayData.ImageType.Shindo, shindoData);
		if (pgaData != null)
			images.Add(KyoshinMonitorImageReplayData.ImageType.Pga, pgaData);

		if (images.Count > 0)
		{
			result.Add(new KyoshinMonitorImageReplayData
			{
				Time = targetTime.AddSeconds(1),
				Images = images,
			});
		}

		return (result, partialFailures);
	}

	/// <summary>
	/// リトライ付きでHTTPリクエストを実行する
	/// </summary>
	private static async Task<byte[]?> FetchWithRetryAsync(HttpClient httpClient, string url, CancellationToken cancellationToken, int maxRetries = 30)
	{
		for (var retry = 0; retry < maxRetries; retry++)
		{
			try
			{
				using var response = await httpClient.GetAsync(url, cancellationToken);
				if (response.StatusCode == HttpStatusCode.OK)
					return await response.Content.ReadAsByteArrayAsync(cancellationToken);

				if (retry < maxRetries - 1)
					await Task.Delay(500, cancellationToken);
			}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				if (retry < maxRetries - 1)
					await Task.Delay(300, cancellationToken);
			}
			catch (HttpRequestException)
			{
				if (retry < maxRetries - 1)
					await Task.Delay(300, cancellationToken);
			}
		}

		return null;
	}

	/// <summary>
	/// ファイルを保存する
	/// </summary>
	private static async Task SaveFileAsync(DateTime intervalStart, List<ReplayData> data)
	{
		var filePath = GetFilePath(intervalStart);

		// 日付フォルダを作成
		var directory = Path.GetDirectoryName(filePath);
		if (directory != null && !Directory.Exists(directory))
			Directory.CreateDirectory(directory);

		var header = new ReplayFileHeader
		{
			SoftwareName = "KyoshinMonitorRecorder",
			CompressionMode = ReplayFileCompressionMode.GZip,
			StartTime = data.Min(d => d.Time),
			EndTime = data.Max(d => d.Time),
		};

		using var stream = new KyoshinReplayFileReader(File.Open(filePath, FileMode.Create));
		await stream.WriteHeader(header);
		await stream.WriteData(data.OrderBy(d => d.Time).ToArray(), header.CompressionMode);
	}

	/// <summary>
	/// ファイルパスを取得する（日付フォルダ含む）
	/// </summary>
	private static string GetFilePath(DateTime intervalStart)
		=> Path.Combine(OutputDirectory, intervalStart.ToString("yyyyMMdd"), $"{intervalStart:HHmm}.eqrp");

	/// <summary>
	/// ファイル名を取得する（表示用）
	/// </summary>
	private static string GetFileName(DateTime intervalStart)
		=> $"{intervalStart:yyyyMMdd}/{intervalStart:HHmm}.eqrp";

	/// <summary>
	/// 時刻を10分単位に切り捨てる
	/// </summary>
	private static DateTime RoundDownToInterval(DateTime time)
	{
		var minute = (time.Minute / SplitIntervalMinutes) * SplitIntervalMinutes;
		return new DateTime(time.Year, time.Month, time.Day, time.Hour, minute, 0);
	}

	/// <summary>
	/// HttpClientを作成する
	/// </summary>
	private static HttpClient CreateHttpClient()
		=> new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
		{
			Timeout = TimeSpan.FromSeconds(2)
		};
}
