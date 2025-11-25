using DmdataSharp;
using DmdataSharp.Authentication.OAuth;
using EarthquakeReplayFilePacker;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinMonitorLib.UrlGenerator;
using Sharprompt;
using Sharprompt.Fluent;
using System.Net;
using System.Text.Json;

await OpenFile();

async Task OpenFile()
{
	var header = new ReplayFileHeader
	{
		SoftwareName = "KyoshinEewViewerIngen-EarthquakeReplayFilePacker",
		CompressionMode = ReplayFileCompressionMode.GZip,
	};
	var data = new List<ReplayData>();

	while (true)
	{
		Console.WriteLine($"\n**現在のデータ\n総数:{data.Count}");

		var select2 = Prompt.Select<string>(o => o.WithMessage("操作を選んでください").WithItems([
			"ファイル読み込み",
			"データ確認",
			"強震モニタの画像を自動組み込み",
			"強震モニタの画像を期間指定で自動取得",
			"強震モニタをリアルタイム受信",
			"dmdataのEEW電文自動組み込み",
			//"JmaXmlTelegramReplayData",
			//"SNPLogEntryReplayData",
			//"AxisJsonReplayData",
			"テスト用データ生成",
			"保存して終了",
			"動作確認",
		]).WithDefaultValue("ファイル読み込み"));

		switch (select2)
		{
			case "ファイル読み込み":
				var loadPath = Prompt.Input<string>("ファイルパスを入力してください");
				if (!File.Exists(loadPath))
				{
					Console.WriteLine("ファイルが見つかりませんでした");
					break;
				}
				using (var stream = new KyoshinReplayFileReader(File.Open(loadPath, FileMode.Open)))
				{
					var loadedHeader = await stream.ReadHeader();
					Console.WriteLine("\n***読み込まれたファイルの情報***");
					Console.WriteLine($"Version: {loadedHeader.Version}");
					Console.WriteLine($"SoftwareName: {loadedHeader.SoftwareName}");
					Console.WriteLine($"StartTime: {loadedHeader.StartTime}");
					Console.WriteLine($"EndTime: {loadedHeader.EndTime}");
					Console.WriteLine($"CompressionMode: {loadedHeader.CompressionMode}");
					if (Prompt.Confirm("結合しますか？"))
						data.AddRange(await stream.ReadData(loadedHeader.CompressionMode));
					else
						data = (await stream.ReadData(loadedHeader.CompressionMode)).ToList();
				}
				break;
			case "データ確認":
				Read(header, data);
				break;
			case "強震モニタの画像を自動組み込み":
				await ImportKyoshinMonitorImage(data);
				break;
			case "強震モニタの画像を期間指定で自動取得":
				await FetchKyoshinMonitorData(data);
				break;
			case "強震モニタをリアルタイム受信":
				await RealtimeKyoshinMonitorReceive();
				break;
			case "dmdataのEEW電文自動組み込み":
				await ImportDmdataEewTelegram(data);
				break;
			case "JmaXmlTelegramReplayData":
				var xmlPath = Prompt.Input<string>("XML ファイルパスを入力してください");
				using (var stream = File.OpenRead(xmlPath))
				using (var jmaxml = new JmaXmlDocument(stream))
				{
					data.Add(new JmaXmlTelegramReplayData
					{
						Time = jmaxml.Control.DateTime.LocalDateTime,
						Title = jmaxml.Control.Title,
						Telegram = await File.ReadAllTextAsync(xmlPath),
					});
				}
				break;
			case "SNPLogEntryReplayData":
				data.Add(new SNPLogEntryReplayData
				{
					Time = DateTime.Now,
					Message = Prompt.Input<string>("ログの内容を入力してください"),
				});
				break;
			case "AxisJsonReplayData":
				data.Add(new AxisJsonReplayData
				{
					Time = DateTime.Now,
					Json = Prompt.Input<string>("Json を入力してください"),
				});
				break;
			case "テスト用データ生成":
				CreateTestData(data);
				break;
			case "保存して終了":
				var savePath = Prompt.Input<string>("保存先のファイルパスを入力してください");
				if (File.Exists(savePath) && !Prompt.Confirm("上書きしますか？"))
					break;
				using (var stream = new KyoshinReplayFileReader(File.Open(savePath, FileMode.Create)))
				{
					header.StartTime = data.Min(d => d.Time);
					header.EndTime = data.Max(d => d.Time);
					await stream.WriteHeader(header);
					await stream.WriteData(data.OrderBy(d => d.Time).ToArray(), header.CompressionMode);
				}
				Console.WriteLine("保存しました");
				return;
			case "動作確認":
				var runner = new ReplayFileRunner(data) { SpeedMultiplier = 2 };
				runner.DataArrived += (time, datas) =>
				{
					Console.WriteLine($"Time: {time:yyyy/MM/dd HH:mm:ss.ffff}");
					foreach (var d in datas)
						Console.WriteLine($"  Type: {d.GetType().Name}");
				};
				runner.Finished += time => Console.WriteLine($"Finished: {time}");
				runner.Start();
				Console.WriteLine("動作確認中... Enter で終了");
				Console.ReadLine();
				await runner.StopAsync();
				break;
		}
	}
}


void Read(ReplayFileHeader header, List<ReplayData> data)
{
	Console.WriteLine("\n***ヘッダ情報***");
	Console.WriteLine($"Version: {header.Version}");
	Console.WriteLine($"SoftwareName: {header.SoftwareName}");
	Console.WriteLine($"StartTime: {header.StartTime}");
	Console.WriteLine($"EndTime: {header.EndTime}");
	Console.WriteLine($"CompressionMode: {header.CompressionMode}");

	Console.WriteLine("\n***データ情報***");
	Console.WriteLine($"データ数: {data.Count}");

	while (true)
	{
		var index = Prompt.Input<int>("見たい index を入力してください(範囲外で終了)", defaultValue: -1);
		if (index < 0 || index >= data.Count)
			break;

		switch (data[index])
		{
			case JmaXmlTelegramReplayData jmaXmlTelegram:
				Console.WriteLine("JmaXmlTelegramReplayData");
				Console.WriteLine($"  Time: {jmaXmlTelegram.Time}");
				Console.WriteLine($"  Title: {jmaXmlTelegram.Title}");
				Console.WriteLine($"  Telegram: {jmaXmlTelegram.Telegram}");
				break;
			case KyoshinMonitorImageReplayData kyoshinMonitorImage:
				Console.WriteLine("KyoshinMonitorImageReplayData");
				Console.WriteLine($"  Time: {kyoshinMonitorImage.Time}");
				foreach (var (key, value) in kyoshinMonitorImage.Images)
					Console.WriteLine($"  ImageType: {key}, ImageSize: {value.Length}");
				break;
			case KyoshinMonitorEewJsonReplayData kyoshinMonitorEewJson:
				Console.WriteLine("KyoshinMonitorEewJsonReplayData");
				Console.WriteLine($"  Time: {kyoshinMonitorEewJson.Time}");
				Console.WriteLine($"  Json: {kyoshinMonitorEewJson.Json}");
				break;
			case KEViJsonReplayData keViJson:
				Console.WriteLine("KEViJsonReplayData");
				Console.WriteLine($"  Time: {keViJson.Time}");
				Console.WriteLine($"  Type: {keViJson.Type}");
				Console.WriteLine($"  Json: {keViJson.Json}");
				break;
			case SNPLogEntryReplayData snpLogEntry:
				Console.WriteLine("SNPLogEntryReplayData");
				Console.WriteLine($"  Time: {snpLogEntry.Time}");
				Console.WriteLine($"  Message: {snpLogEntry.Message}");
				break;
			case AxisJsonReplayData axisJson:
				Console.WriteLine("AxisJsonReplayData");
				Console.WriteLine($"  Time: {axisJson.Time}");
				Console.WriteLine($"  Json: {axisJson.Json}");
				break;
		}
	}
}
async Task ImportKyoshinMonitorImage(List<ReplayData> data)
{
	var directory = Prompt.Input<string>("起点となるフォルダのパスを入力してください");

	// NOTE あんまり汎用的な仕組みにしてない

	// EEW の json をベースに範囲を探索する
	var minTime = DateTime.MaxValue;
	var maxTime = DateTime.MinValue;

	foreach (var file in Directory.EnumerateFiles(Path.Combine(directory, "webservice/hypo/eew"), "*.json"))
	{
		var time = DateTime.ParseExact(Path.GetFileNameWithoutExtension(file), "yyyyMMddHHmmss", null);
		if (time < minTime)
			minTime = time;
		if (time > maxTime)
			maxTime = time;
	}

	Console.WriteLine($"範囲: {minTime} ～ {maxTime} ({(int)(maxTime - minTime).TotalSeconds}秒)");

	var diffSeconds = (int)(maxTime - minTime).TotalSeconds;

	Console.WriteLine("データ追加中...");
	for (var i = 0; i < diffSeconds + 1; i++)
	{
		var time = minTime.AddSeconds(i);

		// json
		data.Add(new KyoshinMonitorEewJsonReplayData()
		{
			// 強震モニタのオフセットを入れる
			Time = time.AddSeconds(1),
			Json = await File.ReadAllTextAsync(Path.Combine(directory, "webservice/hypo/eew", time.ToString("yyyyMMddHHmmss") + ".json")),
		});

		// 画像
		var images = new Dictionary<KyoshinMonitorImageReplayData.ImageType, byte[]>();

		// リアルタイム震度
		if (File.Exists(Path.Combine(directory, "data/map_img/RealTimeImg/jma_s", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".jma_s.gif")))
			images.Add(KyoshinMonitorImageReplayData.ImageType.Shindo, await File.ReadAllBytesAsync(Path.Combine(directory, "data/map_img/RealTimeImg/jma_s", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".jma_s.gif")));
		// 最大加速度
		if (File.Exists(Path.Combine(directory, "data/map_img/RealTimeImg/acmap_s", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".acmap_s.gif")))
			images.Add(KyoshinMonitorImageReplayData.ImageType.Pga, await File.ReadAllBytesAsync(Path.Combine(directory, "data/map_img/RealTimeImg/acmap_s", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".acmap_s.gif")));
		// EEW 予想震度
		if (File.Exists(Path.Combine(directory, "data/map_img/EstShindoImg/eew", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".eew.gif")))
			images.Add(KyoshinMonitorImageReplayData.ImageType.EstShindo, await File.ReadAllBytesAsync(Path.Combine(directory, "data/map_img/EstShindoImg/eew", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".eew.gif")));
		// P/S 波
		if (File.Exists(Path.Combine(directory, "data/map_img/PSWaveImg/eew", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".eew.gif")))
			images.Add(KyoshinMonitorImageReplayData.ImageType.PSWave, await File.ReadAllBytesAsync(Path.Combine(directory, "data/map_img/PSWaveImg/eew", time.ToString("yyyyMMdd"), time.ToString("yyyyMMddHHmmss") + ".eew.gif")));

		if (images.Count > 0)
			data.Add(new KyoshinMonitorImageReplayData
			{
				// 強震モニタのオフセットを入れる
				Time = time.AddSeconds(1),
				Images = images,
			});

		Console.Write($"\r{i}/{diffSeconds}");
	}
}

async Task FetchKyoshinMonitorData(List<ReplayData> data)
{
	Console.WriteLine("強震モニタから指定期間のデータを自動取得します。");
	Console.WriteLine("形式: yyyy/MM/dd HH:mm:ss");

	var startTime = Prompt.Input<DateTime>("開始日時を入力してください");
	var endTime = Prompt.Input<DateTime>("終了日時を入力してください");

	if (startTime >= endTime)
	{
		Console.WriteLine("エラー: 開始日時は終了日時より前である必要があります。");
		return;
	}

	var diffSeconds = (int)(endTime - startTime).TotalSeconds;
	Console.WriteLine($"範囲: {startTime} ～ {endTime} ({diffSeconds}秒)");

	if (!Prompt.Confirm("この期間のデータを取得しますか？"))
		return;

	using var httpClient = new HttpClient(new HttpClientHandler()
	{
		AutomaticDecompression = DecompressionMethods.All
	})
	{ Timeout = TimeSpan.FromSeconds(10) };

	var successCount = 0;
	var failureCount = 0;

	Console.WriteLine("データ取得中...");

	for (var i = 0; i <= diffSeconds; i++)
	{
		var time = startTime.AddSeconds(i);

		try
		{
			// EEW JSON を取得
			var eewJsonUrl = WebApiUrlGenerator.Generate(WebApiUrlType.EewJson, time);
			var eewJson = await FetchWithRetryAsync(httpClient, eewJsonUrl, "EEW JSON", time);

			// リアルタイム震度を取得
			var shindoUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, time, RealtimeDataType.Shindo, false);
			var shindoData = await FetchWithRetryAsync(httpClient, shindoUrl, "リアルタイム震度", time);

			// 最大加速度を取得
			var pgaUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, time, RealtimeDataType.Pga, false);
			var pgaData = await FetchWithRetryAsync(httpClient, pgaUrl, "最大加速度", time);

			// EEW JSON を追加
			if (eewJson != null)
			{
				var jsonString = System.Text.Encoding.UTF8.GetString(eewJson);
				data.Add(new KyoshinMonitorEewJsonReplayData()
				{
					Time = time.AddSeconds(1),
					Json = jsonString,
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
				data.Add(new KyoshinMonitorImageReplayData
				{
					Time = time.AddSeconds(1),
					Images = images,
				});
			}

			successCount++;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"\n{time:yyyy/MM/dd HH:mm:ss} の取得に失敗しました: {ex.Message}");
			failureCount++;
		}

		Console.Write($"\r進捗: {i + 1}/{diffSeconds + 1} (成功: {successCount}, 失敗: {failureCount})");

		// リクエスト間隔を200ms空ける
		await Task.Delay(200);
	}

	Console.WriteLine($"\n\n取得完了: 成功 {successCount}件, 失敗 {failureCount}件");
}

async Task<byte[]?> FetchWithRetryAsync(HttpClient client, string url, string dataType, DateTime time, int maxRetries = 60)
{
	for (var retry = 0; retry < maxRetries; retry++)
	{
		try
		{
			using var response = await client.GetAsync(url);
			if (response.StatusCode == HttpStatusCode.OK)
				return await response.Content.ReadAsByteArrayAsync();

			// その他のエラーはリトライ
			if (retry < maxRetries - 1)
			{
				Console.WriteLine($"\n{time:HH:mm:ss} {dataType} の取得に失敗しました (ステータス: {response.StatusCode})。リトライします...");
				await Task.Delay(10000);
			}
		}
		catch (TaskCanceledException)
		{
			// タイムアウトの場合はリトライ
			if (retry < maxRetries - 1)
			{
				Console.WriteLine($"\n{time:HH:mm:ss} {dataType} の取得がタイムアウトしました。リトライします...");
				await Task.Delay(500);
			}
			else
			{
				throw new Exception($"{dataType} の取得がタイムアウトしました");
			}
		}
		catch (HttpRequestException ex)
		{
			// HTTP通信エラーの場合はリトライ
			if (retry < maxRetries - 1)
			{
				Console.WriteLine($"\n{time:HH:mm:ss} {dataType} の取得中にエラーが発生しました。リトライします...");
				await Task.Delay(500);
			}
			else
			{
				throw new Exception($"{dataType} の取得中にエラーが発生しました: {ex.Message}");
			}
		}
	}

	return null;
}

async Task RealtimeKyoshinMonitorReceive()
{
	Console.WriteLine("強震モニタのリアルタイム受信を開始します。");
	Console.WriteLine("1分前のデータを継続的に取得し、1時間ごとにファイルに保存します。");
	Console.WriteLine("停止するには Ctrl+C を押してください。\n");

	var outputDirectory = Prompt.Input<string>("保存先フォルダのパスを入力してください");

	if (!Directory.Exists(outputDirectory))
	{
		Console.WriteLine("フォルダが存在しません。作成しますか？");
		if (Prompt.Confirm("フォルダを作成"))
			Directory.CreateDirectory(outputDirectory);
		else
			return;
	}

	using var httpClient = new HttpClient(new HttpClientHandler()
	{
		AutomaticDecompression = DecompressionMethods.All
	})
	{ Timeout = TimeSpan.FromSeconds(2) };

	var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (sender, e) =>
	{
		e.Cancel = true;
		cts.Cancel();
		Console.WriteLine("\n停止要求を受け付けました。現在のデータを保存して終了します...");
	};

	var currentHourData = new List<ReplayData>();
	var currentHour = DateTime.MinValue;
	var totalReceived = 0;
	var totalSaved = 0;

	// 開始時刻を1分前に設定（秒単位に丸める）
	var currentTargetTime = DateTime.Parse("2025/11/25 18:00:00");//DateTime.Now.AddMinutes(-10);
	currentTargetTime = new DateTime(currentTargetTime.Year, currentTargetTime.Month, currentTargetTime.Day,
		currentTargetTime.Hour, currentTargetTime.Minute, currentTargetTime.Second);

	try
	{
		Console.WriteLine($"受信を開始しました。開始時刻: {currentTargetTime:yyyy/MM/dd HH:mm:ss}\n");

		while (!cts.Token.IsCancellationRequested)
		{
			var targetTime = currentTargetTime;

			// 現在時刻の1分前を超えないように制限
			var maxTargetTime = DateTime.Now.AddMinutes(-1);
			maxTargetTime = new DateTime(maxTargetTime.Year, maxTargetTime.Month, maxTargetTime.Day,
				maxTargetTime.Hour, maxTargetTime.Minute, maxTargetTime.Second);

			// まだ取得可能な時刻でない場合は待機
			if (targetTime > maxTargetTime)
			{
				await Task.Delay(100, cts.Token);
				continue;
			}

			// 時間が変わったら保存
			var targetHour = new DateTime(targetTime.Year, targetTime.Month, targetTime.Day, targetTime.Hour, 0, 0);
			if (currentHour != DateTime.MinValue && targetHour != currentHour && currentHourData.Count > 0)
			{
				await SaveHourlyFile(outputDirectory, currentHour, currentHourData);
				totalSaved += currentHourData.Count;
				Console.WriteLine($"\n保存完了: {currentHour:yyyy/MM/dd HH:00} ({currentHourData.Count}件)");
				currentHourData.Clear();
			}
			currentHour = targetHour;

			try
			{
				var fetchStartTime = DateTime.Now;

				// EEW JSON を取得
				var eewJsonUrl = WebApiUrlGenerator.Generate(WebApiUrlType.EewJson, targetTime);
				var eewJson = await FetchWithRetryAsync(httpClient, eewJsonUrl, "EEW JSON", targetTime, maxRetries: 10);

				// リアルタイム震度を取得
				var shindoUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, targetTime, RealtimeDataType.Shindo, false);
				var shindoData = await FetchWithRetryAsync(httpClient, shindoUrl, "リアルタイム震度", targetTime, maxRetries: 10);

				// 最大加速度を取得
				var pgaUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, targetTime, RealtimeDataType.Pga, false);
				var pgaData = await FetchWithRetryAsync(httpClient, pgaUrl, "最大加速度", targetTime, maxRetries: 10);

				// EEW JSON を追加
				if (eewJson != null)
				{
					var jsonString = System.Text.Encoding.UTF8.GetString(eewJson);
					currentHourData.Add(new KyoshinMonitorEewJsonReplayData()
					{
						Time = targetTime.AddSeconds(1),
						Json = jsonString,
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
					currentHourData.Add(new KyoshinMonitorImageReplayData
					{
						Time = targetTime.AddSeconds(1),
						Images = images,
					});
				}

				totalReceived++;
				currentTargetTime = targetTime.AddSeconds(1);

				Console.Write($"\r受信: {targetTime:yyyy/MM/dd HH:mm:ss} | 総受信: {totalReceived}件 | 保存済: {totalSaved}件 | バッファ: {currentHourData.Count}件");

				// 取得にかかった時間を計算し、遅延がある場合は待機時間を短縮
				var fetchDuration = DateTime.Now - fetchStartTime;
				var delay = Math.Max(100, 1000 - (int)fetchDuration.TotalMilliseconds);

				if (delay > 200 && (DateTime.Now - currentTargetTime) > TimeSpan.FromMinutes(2))
					delay = 500;

				if (delay > 0)
					await Task.Delay(delay, cts.Token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"\n{targetTime:yyyy/MM/dd HH:mm:ss} の取得に失敗しました: {ex.Message}");
				currentTargetTime = targetTime.AddSeconds(1);
				await Task.Delay(1000, cts.Token);
			}
		}
	}
	catch (OperationCanceledException)
	{
		// Ctrl+C による正常な終了
	}
	finally
	{
		// 残りのデータを保存
		if (currentHourData.Count > 0)
		{
			await SaveHourlyFile(outputDirectory, currentHour, currentHourData);
			totalSaved += currentHourData.Count;
			Console.WriteLine($"\n最終保存: {currentHour:yyyy/MM/dd HH:00} ({currentHourData.Count}件)");
		}

		Console.WriteLine($"\n受信を終了しました。総受信: {totalReceived}件, 総保存: {totalSaved}件");
	}
}

async Task SaveHourlyFile(string directory, DateTime hour, List<ReplayData> data)
{
	var fileName = $"{hour:yyyyMMdd_HH}.eqrp";
	var filePath = Path.Combine(directory, fileName);

	var header = new ReplayFileHeader
	{
		SoftwareName = "KyoshinEewViewerIngen-EarthquakeReplayFilePacker",
		CompressionMode = ReplayFileCompressionMode.GZip,
		StartTime = data.Min(d => d.Time),
		EndTime = data.Max(d => d.Time),
	};

	using var stream = new KyoshinReplayFileReader(File.Open(filePath, FileMode.Create));
	await stream.WriteHeader(header);
	await stream.WriteData(data.OrderBy(d => d.Time).ToArray(), header.CompressionMode);
}

async Task ImportDmdataEewTelegram(List<ReplayData> data)
{
	Console.WriteLine("dmdata の認証を行います。");
	try
	{
		var builder = DmdataApiClientBuilder.Default;
		var credential = await SimpleOAuthAuthenticator.AuthorizationAsync(
			builder.HttpClient,
			"CId.OyFl1d9-Q9ov2sTRwh9Wkk7xzrM1ANyxMnDLmsN7zWvt",
			["eew.get.forecast", "eew.get.realtime", "eew.get.warning", "gd.earthquake", "gd.eew", "telegram.data", "telegram.get.earthquake", "telegram.list"],
			"KyoshinEewViewerIngen-EarthquakeReplayFilePacker",
			url => Console.WriteLine($"以下の URL にアクセスして認証を行ってください。\n{url}")
		);
		try
		{
			var client = builder.UseOAuth(credential).UserAgent("KyoshinEewViewerIngen-EarthquakeReplayFilePacker").BuildV2ApiClient();

			var minTime = data.Min(d => d.Time);
			var maxTime = data.Max(d => d.Time);
			string? cursor = null;

			while (true)
			{
				var events = await client.GetEewEventsAsync(minTime, maxTime, cursor);
				if (events.Items.Length <= 0 && cursor == null)
				{
					Console.WriteLine("データが見つかりませんでした");
					break;
				}

				foreach (var eew in events.Items)
				{
					Console.WriteLine($"\n発見: {eew.EventId}");

					var evt = await client.GetEewEventAsync(eew.EventId);
					var count = 0;
					foreach (var eewItem in evt.Items)
					{
						var telegram = eewItem.Telegrams.First();

						Console.Write($"\r {++count}/{evt.Items.Length}");
						data.Add(new JmaXmlTelegramReplayData
						{
							// NOTE: 9時間足して日本時間に変換している
							Time = telegram.ReceivedTime.AddHours(9),
							Title = telegram.XmlReport.Control.Title,
							Telegram = await client.GetTelegramStringAsync(telegram.OriginalId),
						});

						await Task.Delay(100); // 負荷対策
					}
				}

				if (events.NextToken == null)
					break;
				cursor = events.NextToken;
			}
		}
		finally
		{
			await credential.RevokeRefreshTokenAsync();
			Console.WriteLine("リフレッシュトークンを無効化しました");
		}
	}
	catch (Exception e)
	{
		Console.WriteLine("dmdata の組み込みに失敗しました");
		Console.WriteLine(e.Message);
	}

	Console.WriteLine("dmdata の組み込みが完了しました");
}

void CreateTestData(List<ReplayData> data)
{
	var baseTime = DateTime.MinValue;
	var testTargets = new List<(DateTime time, KeviEew eew)>() {
		(baseTime.AddSeconds(0), new()
		{
			Id = "1",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "SerialNo上書きテスト 段階1",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime,
			},
			IsFinal = false,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.KyoshinMonitor,
		}),
		(baseTime.AddSeconds(5), new()
		{
			Id = "1",
			DisplaySource = "SerialNoで上書きするテスト",
			Hypocenter = new()
			{
				Place = "このままでいればOK",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime,
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 2,
			Source = EewSource.KyoshinMonitor,
		}),
		(baseTime.AddSeconds(10), new()
		{
			Id = "1",
			DisplaySource = "SerialNoで上書きするテスト",
			Hypocenter = new()
			{
				Place = "これが見えたらアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime,
			},
			IsFinal = true,
			IsWarning = true,
			ReceiveTime = baseTime,
			SerialNo = 2,
			Source = EewSource.KyoshinMonitor,
		}),


		(baseTime.AddSeconds(15), new()
		{
			Id = "2",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "キャンセルテスト 段階1",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(1),
			},
			IsFinal = false,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.KyoshinMonitor,
		}),
		(baseTime.AddSeconds(20), new()
		{
			Id = "2",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "キャンセルテスト 段階2",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(1),
			},
			IsFinal = false,
			IsWarning = false,
			IsCancelled = true,
			ReceiveTime = baseTime,
			SerialNo = 2,
			Source = EewSource.KyoshinMonitor,
		}),
		(baseTime.AddSeconds(25), new()
		{
			Id = "2",
			DisplaySource = "キャンセルテスト",
			Hypocenter = new()
			{
				Place = "裏からうっすら見えるはず",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(1),
			},
			IsFinal = false,
			IsWarning = false,
			IsCancelled = false,
			ReceiveTime = baseTime,
			SerialNo = 3,
			Source = EewSource.KyoshinMonitor,
		}),

		(baseTime.AddSeconds(30), new()
		{
			Id = "3",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "kmoni -> DMDATA テスト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(2),
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.KyoshinMonitor,
		}),
		(baseTime.AddSeconds(35), new()
		{
			Id = "3",
			DisplaySource = "kmoni -> DMDATA テスト",
			Hypocenter = new()
			{
				Place = "これが見えていればOK",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(2),
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.Dmdata,
		}),

		(baseTime.AddSeconds(40), new()
		{
			Id = "4",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "DMDATA -> kmoni テスト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(4),
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.Dmdata,
		}),
		(baseTime.AddSeconds(45), new()
		{
			Id = "4",
			DisplaySource = "DMDATA -> kmoni テスト",
			Hypocenter = new()
			{
				Place = "これは見えていればアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(4),
			},
			IsFinal = true,
			IsWarning = true,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.KyoshinMonitor,
		}),

		(baseTime.AddSeconds(50), new()
		{
			Id = "5",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "kmoni -> SNP テスト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = false,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(5),
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.KyoshinMonitor,
		}),
		(baseTime.AddSeconds(55), new()
		{
			Id = "5",
			DisplaySource = "kmoni -> SNP テスト",
			Hypocenter = new()
			{
				Place = "これは見えていればアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = false,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(5),
				Accuracy = new()
				{
					DepthAccuracy = 1,
					LocationAccuracy = 1,
					MagnitudeAccuracy = 1,
					IsLocked = false,
				},
			},
			IsFinal = true,
			IsWarning = true,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.SignalNowProfessional,
		}),

		(baseTime.AddSeconds(60), new()
		{
			Id = "6",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "SNP -> kmoni テスト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = false,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(6),
				Accuracy = new()
				{
					DepthAccuracy = 1,
					LocationAccuracy = 1,
					MagnitudeAccuracy = 1,
					IsLocked = false,
				},
			},
			IsFinal = true,
			IsWarning = true,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.SignalNowProfessional,
		}),
		(baseTime.AddSeconds(65), new()
		{
			Id = "6",
			DisplaySource = "SNP -> kmoni テスト",
			Hypocenter = new()
			{
				Place = "精度情報が見えていなければアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = false,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(6),
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.KyoshinMonitor,
		}),

		(baseTime.AddSeconds(70), new()
		{
			Id = "7",
			DisplaySource = "",
			Hypocenter = new()
			{
				Place = "SNP -> DMDATA テスト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(7),
				Accuracy = new()
				{
					DepthAccuracy = 1,
					LocationAccuracy = 1,
					MagnitudeAccuracy = 1,
					IsLocked = false,
				},
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.SignalNowProfessional,
		}),
		(baseTime.AddSeconds(75), new()
		{
			Id = "7",
			DisplaySource = "SNP -> DMDATA テスト",
			Hypocenter = new()
			{
				Place = "精度情報が見えていればアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = false,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(7),
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.Dmdata,
		}),

		(baseTime.AddSeconds(80), new()
		{
			Id = "8",
			DisplaySource = "警報電文のマージテスト",
			Hypocenter = new()
			{
				Place = "警報地域が見えていなければアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = false,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(8),
			},
			IsFinal = true,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 2,
			Source = EewSource.KyoshinMonitor,
		}),
	};



	foreach (var (time, eew) in testTargets)
	{
		data.Add(new KEViJsonReplayData
		{
			Time = time,
			Type = KEViJsonReplayData.JsonType.Eew,
			Json = JsonSerializer.Serialize(eew),
		});
	}

	data.Add(new KEViJsonReplayData
	{
		Time = baseTime.AddSeconds(85),
		Type = KEViJsonReplayData.JsonType.EewWarning,
		Json = JsonSerializer.Serialize(new KeviEew
		{
			Id = "8",
			DisplaySource = "警報地域のマージテスト",
			Hypocenter = new()
			{
				Place = "これが見えていればアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(8),
			},
			IsFinal = false,
			IsWarning = true,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.Dmdata,
			WarningAreas = new()
			{
				DisplaySource = "SignalNowProfessional",
				Codes = [1, 2, 3],
				Names = ["警報地域1", "警報地域2", "警報地域3"],
			},
		}),
	});

	data.Add(new KEViJsonReplayData
	{
		Time = baseTime.AddSeconds(90),
		Type = KEViJsonReplayData.JsonType.EewWarning,
		Json = JsonSerializer.Serialize(new KeviEew
		{
			Id = "9",
			DisplaySource = "警報単独テスト",
			Hypocenter = new()
			{
				Place = "これが見えていなければアウト",
				Depth = 0,
				Magnitude = 0,
				IsTemporary = true,
				Location = new(0, 0),
				OccurrenceTime = baseTime.AddSeconds(9),
			},
			IsFinal = false,
			IsWarning = false,
			ReceiveTime = baseTime,
			SerialNo = 1,
			Source = EewSource.Dmdata,
			WarningAreas = new()
			{
				DisplaySource = "SignalNowProfessional",
				Codes = [1, 2, 3],
				Names = ["警報地域1", "警報地域2", "警報地域3"],
			},
		}),
	});
}
