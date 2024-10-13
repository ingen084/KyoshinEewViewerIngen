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
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

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
	private SignalNowEew? ParseData(string rawData)
	{
		try
		{
			if (rawData.Length <= 67)
				return null;

			var eew = new SignalNowEew
			{
				IsCancelled = rawData[4..6] == "10",
				ReceiveTime = DateTime.ParseExact($"20{rawData[6..8]}/{rawData[8..10]}/{rawData[10..12]} {rawData[12..14]}:{rawData[14..16]}:{rawData[16..18]}", "yyyy/MM/dd HH:mm:ss", null),
				OccurrenceTime = DateTime.ParseExact($"20{rawData[18..20]}/{rawData[20..22]}/{rawData[22..24]} {rawData[24..26]}:{rawData[26..28]}:{rawData[28..30]}", "yyyy/MM/dd HH:mm:ss", null),
				Id = rawData[30..46][2..], // 先頭2文字を削る
				IsFinal = rawData[46] == '9',
				UpdatedTime = Timer.CurrentTime,
			};
			if (int.TryParse(rawData[47..49], out var c))
				eew.Count = c;
			if (float.TryParse(rawData[50..53], out var lat) && float.TryParse(rawData[54..58], out var lng))
				eew.Location = new Location(lat / 10, lng / 10);
			if (int.TryParse(rawData[58..61], out var d))
				eew.Depth = d;
			if (float.TryParse(rawData[61..63], out var m))
				eew.Magnitude = m / 10;
			if (int.TryParse(rawData[63..64], out var la))
				eew.LocationAccuracy = la;
			if (int.TryParse(rawData[64..65], out var da))
				eew.DepthAccuracy = da;
			if (int.TryParse(rawData[65..66], out var ma))
				eew.MagnitudeAccuracy = ma;

			var areas = new List<int>();
			for (var i = 68; i < rawData.Length - 3; i += 3)
				if (int.TryParse(rawData[i..(i + 3)], out var o))
					areas.Add(o);
			if (areas.Count > 0)
				eew.WarningAreaCodes = areas.ToArray();
			return eew;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "SNPログ更新中に問題が発生しました");
			return null;
		}
	}
}

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
public class SignalNowEew : IEew
{
	/// <summary>
	/// 地震ID
	/// </summary>
	public string Id { get; init; }

	/// <summary>
	/// キャンセル報か
	/// </summary>
	public bool IsCancelled { get; init; }
	public bool IsTrueCancelled => IsCancelled;

	/// <summary>
	/// 受信時刻
	/// </summary>
	public DateTime ReceiveTime { get; init; }

	/// <summary>
	/// 地震の発生時間
	/// </summary>
	public DateTime OccurrenceTime { get; init; }
	/// <summary>
	/// 震央座標
	/// </summary>
	public Location Location { get; set; }
	/// <summary>
	/// マグニチュード
	/// </summary>
	public float? Magnitude { get; set; }
	/// <summary>
	/// 震源の深さ
	/// </summary>
	public int Depth { get; set; }
	/// <summary>
	/// 報数
	/// </summary>
	public int Count { get; set; }
	/// <summary>
	/// 最終報か
	/// </summary>
	public bool IsFinal { get; set; }

	public bool IsAccuracyFound => LocationAccuracy != null && DepthAccuracy != null && MagnitudeAccuracy != null;
	/// <summary>
	/// 震央の確からしさフラグ
	/// </summary>
	public int? LocationAccuracy { get; set; }
	/// <summary>
	/// 深さの確からしさフラグ
	/// </summary>
	public int? DepthAccuracy { get; set; }
	/// <summary>
	/// マグニチュードの確からしさフラグ
	/// </summary>
	public int? MagnitudeAccuracy { get; set; }
	// SNPでもこのフラグは存在しないので他の要素から判断する
	public bool IsTemporaryEpicenter => Depth == 10 && Magnitude == 1.0;
	// SNPではこのフラグが送られてこないので null
	public bool? IsLocked => null;

	/// <summary>
	/// 予想震度一覧
	/// </summary>
	public Dictionary<int, JmaIntensity>? ForecastIntensityMap { get; set; }

	/// <summary>
	/// 警報地域コード一覧
	/// </summary>
	public int[]? WarningAreaCodes { get; set; }

	/// <summary>
	/// 警報地域名一覧
	/// </summary>
	public string[]? WarningAreaNames { get; set; }

	/// <summary>
	/// 表示する情報元
	/// </summary>
	public string SourceDisplay => "SignalNowProfessional";

	public JmaIntensity Intensity => JmaIntensity.Unknown;
	public bool IsIntensityOver => false;
	public string? Place => "不明(未受信)";
	public bool IsWarning => WarningAreaCodes?.Length > 0;

	public int Priority => -1;

	/// <summary>
	/// ソフトで更新した時刻　内部利用値
	/// </summary>
	public DateTime UpdatedTime { get; set; }

	public bool IsVisible { get; set; } = true;
}
