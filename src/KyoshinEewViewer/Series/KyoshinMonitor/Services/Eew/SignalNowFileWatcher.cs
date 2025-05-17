using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Splat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using ZLinq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

public class SignalNowFileWatcher
{
	public bool CanReceive { get; private set; }

	private const string LogName = "snp.log";
	private const string SettingsName = "setting.xml";
	public static string SnpDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SignalNowProfessional");
	public static string LogPath => Path.Combine(SnpDirectory, LogName);
	public static string SettingsPath => Path.Combine(SnpDirectory, SettingsName);

	public Location? CurrentLocation { get; private set; }

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private EewController EewController { get; }
	private TimerService Timer { get; }
	private FileSystemWatcher? LogfileWatcher { get; set; }
	private FileSystemWatcher? SettingsfileWatcher { get; set; }
	private long LastLogfileSize { get; set; }


	public SignalNowFileWatcher(ILogManager logManager, KyoshinEewViewerConfiguration config, EewController eewControlService, TimerService timer)
	{
		Logger = logManager.GetLogger<SignalNowFileWatcher>();
		Config = config;
		EewController = eewControlService;
		Timer = timer;

		UpdateWatcher();
	}

	private void UpdateWatcher()
	{
		if (LogfileWatcher != null)
		{
			LogfileWatcher.Dispose();
			LogfileWatcher = null;
		}

		var info = new FileInfo(LogPath);
		CanReceive = info.Exists && Config.Eew.EnableSignalNowProfessional;
		if (!CanReceive)
			return;

		LastLogfileSize = info.Length;
		LogfileWatcher = new FileSystemWatcher(SnpDirectory, LogName)
		{
			NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName
		};
		LogfileWatcher.Changed += LogfileChanged;
		LogfileWatcher.EnableRaisingEvents = true;
		Logger.LogInfo("SNPログのWatchを開始しました。");

		if (SettingsfileWatcher != null)
		{
			SettingsfileWatcher.Dispose();
			SettingsfileWatcher = null;
		}

		if (!Config.Eew.EnableSignalNowProfessionalLocation || !File.Exists(SettingsPath))
			return;

		SettingsfileWatcher = new FileSystemWatcher(SnpDirectory, SettingsName)
		{
			NotifyFilter = NotifyFilters.LastWrite
		};
		SettingsfileWatcher.Changed += SettingsFileChanged;
		SettingsfileWatcher.EnableRaisingEvents = true;
		Logger.LogInfo("SNP設定ファイルのWatchを開始しました。");

		Task.Run(() =>
		{
			try
			{
				ProcessLocation().Wait();
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "SNPの設定ファイル解析時にエラーが発生しました");
			}

		}).ConfigureAwait(false);
	}

	private static async Task<StreamReader> TryOpenTextAsync(string path, int maxCount = 10, int waitTime = 10)
	{
		var count = 0;
		while (count < 10)
		{
			try
			{
				return File.OpenText(path);
			}
			catch (IOException)
			{
				await Task.Delay(waitTime);
				maxCount++;
			}
		}
		throw new Exception("SNPログにアクセスできませんでした。");
	}

	private async void LogfileChanged(object sender, FileSystemEventArgs e)
	{
		try
		{
			Logger.LogDebug($"SNPのログファイルが変更されました: {e.ChangeType}");
			// ログが消去(rotate)された場合はウォッチし直す
			if (e.ChangeType == WatcherChangeTypes.Renamed)
			{
				Logger.LogInfo("SNPログのrotateを検出しました。");
				UpdateWatcher();
				return;
			}

			// ファイル操作が完了するのを待つ
			using var reader = await TryOpenTextAsync(LogPath);
			reader.BaseStream.Position = LastLogfileSize;

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine();
				if (line == null || !line.StartsWith("EQ") || !line.Contains("データ受信"))
					continue;
				Logger.LogInfo($"SNPのEEWを受信しました: {line[32..]}");
				var eew = ParseData(line[32..]) ?? throw new Exception("パースに失敗しています");
				EewController.Update(eew, eew.ReceiveTime);
			}

			var info = new FileInfo(LogPath);
			LastLogfileSize = info.Length;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "SNPのログ解析時にエラーが発生しました");
		}
	}

	private async Task ProcessLocation()
	{
		// ファイル操作が完了するのを待つ
		using var reader = await TryOpenTextAsync(SettingsPath);

		var doc = XDocument.Load(reader);
		var lat = doc.XPathSelectElement("/setting/lat") ?? throw new Exception("latが取得できません");
		var lon = doc.XPathSelectElement("/setting/lon") ?? throw new Exception("lonが取得できません");
		var loc = new Location(
			float.Parse(lat.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
			float.Parse(lon.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture));

		CurrentLocation = loc;
	}
	private void SettingsFileChanged(object sender, FileSystemEventArgs e)
	{
		try
		{
			Logger.LogDebug($"SNPの設定ファイルが変更されました: {e.ChangeType}");
			ProcessLocation().Wait();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "SNPの設定ファイル解析時にエラーが発生しました");
		}
	}

	~SignalNowFileWatcher()
	{
		if (LogfileWatcher != null)
		{
			LogfileWatcher.Dispose();
			LogfileWatcher = null;
		}
		if (SettingsfileWatcher != null)
		{
			SettingsfileWatcher.Dispose();
			SettingsfileWatcher = null;
		}
	}

	// 0             1              2              3         4            5           6
	// 0123 45 67 89 01 23 45 67 89 01 23 45 67 89 0123456789012345 6 78 9012 34567 890 12 3 4 5 67
	// 0137 00 21/02/18_21:43:54 21/02/18_21:42:58 ND20210218214309 9 03 N375 E1417 060 38 6 6 2 09
	private Models.Eew? ParseData(string rawData)
	{
		try
		{
			if (rawData.Length <= 67)
				return null;

			var areas = new List<int>();
			for (var i = 68; i < rawData.Length - 3; i += 3)
				if (int.TryParse(rawData[i..(i + 3)], out var o))
					areas.Add(o);

			return new Models.Eew
			{
				DisplaySource = "SignalNowProfessional",
				Source = EewSource.SignalNowProfessional,
				IsCancelled = rawData[4..6] == "10",
				ReceiveTime = Timer.CurrentTime,// DateTime.ParseExact($"20{rawData[6..8]}/{rawData[8..10]}/{rawData[10..12]} {rawData[12..14]}:{rawData[14..16]}:{rawData[16..18]}", "yyyy/MM/dd HH:mm:ss", null),
				Id = rawData[30..46][2..], // 先頭2文字を削る
				IsFinal = rawData[46] == '9',

				SerialNo = int.Parse(rawData[47..49]),

				Hypocenter = new EewHypocenter
				{
					Place = "不明(未受信)",
					OccurrenceTime = DateTime.ParseExact($"20{rawData[18..20]}/{rawData[20..22]}/{rawData[22..24]} {rawData[24..26]}:{rawData[26..28]}:{rawData[28..30]}", "yyyy/MM/dd HH:mm:ss", null),
					Location = new Location(int.Parse(rawData[50..53]) / 10.0f, int.Parse(rawData[54..58]) / 10.0f),
					Depth = int.Parse(rawData[58..61]),
					Magnitude = int.Parse(rawData[61..63]) / 10.0f,
					Accuracy = new EewHypocenterAccuracy
					{
						IsLocked = false,
						LocationAccuracy = int.Parse(rawData[63..64]),
						DepthAccuracy = int.Parse(rawData[64..65]),
						MagnitudeAccuracy = int.Parse(rawData[65..66]),
					},
					IsTemporary = int.Parse(rawData[58..61]) == 10 && int.Parse(rawData[61..63]) == 10,
				},

				WarningAreas = areas.Count > 0 ? new EewWarningAreas
				{
					DisplaySource = "SignalNowProfessional",
					Codes = areas.ToArray(),
					Names = EewAreaCompressor.Compress(areas.Select(a => CsvDictionary.AreaEpicenter.TryGetValue(a, out var p) ? p : $"不明({a})").ToArray()),
				} : null,
				IsWarning = areas.Count > 0,
			};
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "SNPログ更新中に問題が発生しました");
			return null;
		}
	}
}
