using DmdataSharp;
using DmdataSharp.Authentication.OAuth;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinEewViewer.JmaXmlParser;
using Sharprompt;
using Sharprompt.Fluent;

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
			"dmdataのEEW電文自動組み込み",
			"JmaXmlTelegramReplayData",
			"SNPLogEntryReplayData",
			"AxisJsonReplayData",
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
