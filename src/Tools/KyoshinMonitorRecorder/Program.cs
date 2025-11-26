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

		var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (sender, e) =>
		{
			e.Cancel = true;
			cts.Cancel();
			Console.WriteLine("\n停止要求を受け付けました。現在のデータを保存して終了します...");
		};

		// 起動時に欠損データを補完
		await BackfillMissingDataAsync(cts.Token);

		// リアルタイム録画を開始
		await StartRealtimeRecordingAsync(cts.Token);
	}

	/// <summary>
	/// 欠損データを補完する
	/// </summary>
	private static async Task BackfillMissingDataAsync(CancellationToken cancellationToken)
	{
		Console.WriteLine("欠損データの確認を開始します...");

		var now = DateTime.Now;
		var backfillStart = now.AddHours(-BackfillHours);

		// 補完対象の時間範囲を10分単位で算出
		var missingIntervals = GetMissingIntervals(backfillStart, now);

		if (missingIntervals.Count == 0)
		{
			Console.WriteLine("欠損データはありません。");
			return;
		}

		Console.WriteLine($"欠損区間: {missingIntervals.Count}件");

		using var httpClient = CreateHttpClient();

		foreach (var interval in missingIntervals)
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			Console.WriteLine($"\n補完中: {interval.Start:yyyy/MM/dd HH:mm} - {interval.End:yyyy/MM/dd HH:mm}");
			await FetchAndSaveIntervalAsync(httpClient, interval.Start, interval.End, cancellationToken);
		}

		Console.WriteLine("\n欠損データの補完が完了しました。");
	}

	/// <summary>
	/// 欠損している時間区間を取得する
	/// </summary>
	private static List<(DateTime Start, DateTime End)> GetMissingIntervals(DateTime from, DateTime to)
	{
		var missing = new List<(DateTime Start, DateTime End)>();

		// 開始時刻を10分単位に丸める
		var currentStart = RoundDownToInterval(from);

		while (currentStart < to)
		{
			var intervalEnd = currentStart.AddMinutes(SplitIntervalMinutes);
			var fileName = GetFileName(currentStart);
			var filePath = Path.Combine(OutputDirectory, fileName);

			// ファイルが存在しない、または空のファイルは欠損とみなす
			if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
			{
				// 現在より1分以上前のデータのみ補完対象とする
				if (intervalEnd <= DateTime.Now.AddMinutes(-1))
					missing.Add((currentStart, intervalEnd));
			}

			currentStart = intervalEnd;
		}

		return missing;
	}

	/// <summary>
	/// 指定区間のデータを取得して保存する
	/// </summary>
	private static async Task FetchAndSaveIntervalAsync(HttpClient httpClient, DateTime start, DateTime end, CancellationToken cancellationToken)
	{
		var data = new List<ReplayData>();
		var successCount = 0;
		var failureCount = 0;

		var currentTime = start;
		while (currentTime < end && !cancellationToken.IsCancellationRequested)
		{
			try
			{
				var replayData = await FetchKyoshinDataAsync(httpClient, currentTime, cancellationToken);
				if (replayData != null)
				{
					data.AddRange(replayData);
					successCount++;
				}
				else
				{
					failureCount++;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"\n{currentTime:HH:mm:ss} の取得に失敗: {ex.Message}");
				failureCount++;
			}

			currentTime = currentTime.AddSeconds(1);
			Console.Write($"\r進捗: {successCount + failureCount}/{(int)(end - start).TotalSeconds} (成功: {successCount}, 失敗: {failureCount})");

			// リクエスト間隔
			await Task.Delay(300, cancellationToken);
		}

		if (data.Count > 0)
		{
			await SaveFileAsync(start, data);
			Console.WriteLine($"\n保存完了: {GetFileName(start)} ({data.Count}件)");
		}
	}

	/// <summary>
	/// リアルタイム録画を開始する
	/// </summary>
	private static async Task StartRealtimeRecordingAsync(CancellationToken cancellationToken)
	{
		Console.WriteLine("\nリアルタイム録画を開始します。");
		Console.WriteLine("停止するには Ctrl+C を押してください。\n");

		using var httpClient = CreateHttpClient();

		var currentIntervalData = new List<ReplayData>();
		var currentIntervalStart = DateTime.MinValue;
		var totalReceived = 0;
		var totalSaved = 0;

		// 開始時刻を1分前に設定
		var currentTargetTime = DateTime.Now.AddMinutes(-1);
		currentTargetTime = new DateTime(currentTargetTime.Year, currentTargetTime.Month, currentTargetTime.Day,
			currentTargetTime.Hour, currentTargetTime.Minute, currentTargetTime.Second);

		try
		{
			Console.WriteLine($"受信開始時刻: {currentTargetTime:yyyy/MM/dd HH:mm:ss}\n");

			while (!cancellationToken.IsCancellationRequested)
			{
				var targetTime = currentTargetTime;

				// 現在時刻の1分前を超えないように制限
				var maxTargetTime = DateTime.Now.AddMinutes(-1);
				maxTargetTime = new DateTime(maxTargetTime.Year, maxTargetTime.Month, maxTargetTime.Day,
					maxTargetTime.Hour, maxTargetTime.Minute, maxTargetTime.Second);

				if (targetTime > maxTargetTime)
				{
					await Task.Delay(100, cancellationToken);
					continue;
				}

				// 区間が変わったら保存
				var targetIntervalStart = RoundDownToInterval(targetTime);
				if (currentIntervalStart != DateTime.MinValue && targetIntervalStart != currentIntervalStart && currentIntervalData.Count > 0)
				{
					await SaveFileAsync(currentIntervalStart, currentIntervalData);
					totalSaved += currentIntervalData.Count;
					Console.WriteLine($"\n保存完了: {GetFileName(currentIntervalStart)} ({currentIntervalData.Count}件)");
					currentIntervalData.Clear();
				}
				currentIntervalStart = targetIntervalStart;

				try
				{
					var fetchStartTime = DateTime.Now;

					var replayData = await FetchKyoshinDataAsync(httpClient, targetTime, cancellationToken);
					if (replayData != null)
					{
						currentIntervalData.AddRange(replayData);
						totalReceived++;
					}

					currentTargetTime = targetTime.AddSeconds(1);

					Console.Write($"\r受信: {targetTime:yyyy/MM/dd HH:mm:ss} | 総受信: {totalReceived}件 | 保存済: {totalSaved}件 | バッファ: {currentIntervalData.Count}件");

					// 取得にかかった時間を計算し、遅延がある場合は待機時間を短縮
					var fetchDuration = DateTime.Now - fetchStartTime;
					var delay = Math.Max(100, 1000 - (int)fetchDuration.TotalMilliseconds);

					// 2分以上の遅延がある場合は待機時間を短縮
					if (delay > 200 && (DateTime.Now - currentTargetTime) > TimeSpan.FromMinutes(2))
						delay = 500;

					if (delay > 0)
						await Task.Delay(delay, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"\n{targetTime:yyyy/MM/dd HH:mm:ss} の取得に失敗: {ex.Message}");
					currentTargetTime = targetTime.AddSeconds(1);
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
	/// 強震モニタのデータを取得する
	/// </summary>
	private static async Task<List<ReplayData>?> FetchKyoshinDataAsync(HttpClient httpClient, DateTime targetTime, CancellationToken cancellationToken)
	{
		var result = new List<ReplayData>();

		// EEW JSON を取得
		var eewJsonUrl = WebApiUrlGenerator.Generate(WebApiUrlType.EewJson, targetTime);
		var eewJson = await FetchWithRetryAsync(httpClient, eewJsonUrl, cancellationToken);

		// リアルタイム震度を取得
		var shindoUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, targetTime, RealtimeDataType.Shindo, false);
		var shindoData = await FetchWithRetryAsync(httpClient, shindoUrl, cancellationToken);

		// 最大加速度を取得
		var pgaUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, targetTime, RealtimeDataType.Pga, false);
		var pgaData = await FetchWithRetryAsync(httpClient, pgaUrl, cancellationToken);

		if (eewJson == null && shindoData == null && pgaData == null)
			return null;

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

		return result;
	}

	/// <summary>
	/// リトライ付きでHTTPリクエストを実行する
	/// </summary>
	private static async Task<byte[]?> FetchWithRetryAsync(HttpClient httpClient, string url, CancellationToken cancellationToken, int maxRetries = 10)
	{
		for (var retry = 0; retry < maxRetries; retry++)
		{
			try
			{
				using var response = await httpClient.GetAsync(url, cancellationToken);
				if (response.StatusCode == HttpStatusCode.OK)
					return await response.Content.ReadAsByteArrayAsync(cancellationToken);

				if (retry < maxRetries - 1)
					await Task.Delay(1000, cancellationToken);
			}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				if (retry < maxRetries - 1)
					await Task.Delay(500, cancellationToken);
			}
			catch (HttpRequestException)
			{
				if (retry < maxRetries - 1)
					await Task.Delay(500, cancellationToken);
			}
		}

		return null;
	}

	/// <summary>
	/// ファイルを保存する
	/// </summary>
	private static async Task SaveFileAsync(DateTime intervalStart, List<ReplayData> data)
	{
		var fileName = GetFileName(intervalStart);
		var filePath = Path.Combine(OutputDirectory, fileName);

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
	/// ファイル名を取得する
	/// </summary>
	private static string GetFileName(DateTime intervalStart)
		=> $"{intervalStart:yyyyMMdd_HHmm}.eqrp";

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
