using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew
{
	public class SignalNowEewReceiveService
	{
		private static SignalNowEewReceiveService? _default;
		public static SignalNowEewReceiveService Default => _default ??= new();

		public bool CanReceive { get; private set; }

		private const string LOG_NAME = "snp.log";
		public static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SignalNowProfessional");
		public static string LogPath => Path.Combine(LogDirectory, LOG_NAME);

		private ILogger Logger { get; }
		private EewControlService EewController { get; }
		private FileSystemWatcher? LogfileWatcher { get; set; }
		private long LastLogfileSize { get; set; }


		public SignalNowEewReceiveService()
		{
			EewController = EewControlService.Default;
			Logger = LoggingService.CreateLogger(this);

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
			CanReceive = info.Exists && ConfigurationService.Default.Eew.EnableSignalNowProfessional;
			if (!CanReceive)
				return;

			LastLogfileSize = info.Length;
			LogfileWatcher = new FileSystemWatcher(LogDirectory, LOG_NAME)
			{
				NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName
			};
			LogfileWatcher.Changed += LogfileChanged;
			LogfileWatcher.EnableRaisingEvents = true;
			Logger.LogInformation("SNPログのWatchを開始しました。");
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
				Debug.WriteLine("SnpLogfileChanged: " + e.ChangeType);
				// ログが消去(rotate)された場合はウォッチし直す
				if (e.ChangeType == WatcherChangeTypes.Renamed)
				{
					Logger.LogInformation("SNPログのrotateを検出しました。");
					UpdateWatcher();
					return;
				}

				// ファイル操作が完了するのを待つ
				using var reader = await TryOpenTextAsync(LogPath);
				reader.BaseStream.Position = LastLogfileSize;

				while (!reader.EndOfStream)
				{
					var line = await reader.ReadLineAsync();
					if (line == null || !line.StartsWith("EQ") || !line.Contains("データ受信"))
						continue;
					Logger.LogInformation("[SNP] EEW受信: {eewLine}", line[32..]);
					var eew = ParseData(line[32..]);
					if (eew == null)
						throw new Exception("パースに失敗しています");
					EewController.UpdateOrRefreshEew(new Core.Models.Eew(EewSource.SignalNowProfessional, eew.Id[2..])
					{
						Count = eew.Count,
						Depth = eew.Depth,
						IsCancelled = eew.IsCancelled,
						IsFinal = eew.IsFinal,
						IsWarning = eew.WarningAreas.Any(),
						Location = eew.Location,
						Magnitude = eew.Magnitude,
						OccurrenceTime = eew.OccurrenceTime,
						ReceiveTime = eew.ReceiveTime,
						UpdatedTime = eew.ReceiveTime,
						// 1点検知の場合曖昧フラグを立てる
						IsUnreliableLocation = eew.LocationAccuracy == 1,
						IsUnreliableDepth = eew.DepthAccuracy == 1,
						IsUnreliableMagnitude = eew.MagnitudeAccuracy == 1,
					}, eew.ReceiveTime);
				}

				var info = new FileInfo(LogPath);
				LastLogfileSize = info.Length;
			}
			catch (Exception ex)
			{
				Logger.LogError("SNPのログ解析時にエラーが発生しました: {ex}", ex);
			}
		}

		~SignalNowEewReceiveService()
		{
			if (LogfileWatcher == null)
				return;
			LogfileWatcher.Dispose();
			LogfileWatcher = null;
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
					Id = rawData[30..46],
					IsFinal = rawData[46] == '9',
				};
				if (int.TryParse(rawData[47..49], out var c))
					eew.Count = c;
				if (float.TryParse(rawData[50..53], out var lat) && float.TryParse(rawData[54..58], out var lng))
					eew.Location = new(lat / 10, lng / 10);
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

				for (var i = 68; i < rawData.Length - 3; i += 3)
					if (int.TryParse(rawData[i..(i + 3)], out var o))
						eew.WarningAreas.Add(o);
				return eew;
			}
			catch (Exception ex)
			{
				Logger.LogError("SNPログ更新中に問題が発生しました: {ex}", ex);
				return null;
			}
		}
	}

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
	public class SignalNowEew
	{
		/// <summary>
		/// 地震ID
		/// </summary>
		public string Id { get; set; }
		/// <summary>
		/// キャンセル報か
		/// </summary>
		public bool IsCancelled { get; set; }
		/// <summary>
		/// 受信時刻
		/// </summary>
		public DateTime ReceiveTime { get; set; }
		/// <summary>
		/// 地震の発生時間
		/// </summary>
		public DateTime OccurrenceTime { get; set; }
		/// <summary>
		/// 震央座標
		/// </summary>
		public Location Location { get; set; }
		/// <summary>
		/// マグニチュード
		/// </summary>
		public float Magnitude { get; set; }
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

		/// <summary>
		/// 震央の確からしさフラグ
		/// </summary>
		public int LocationAccuracy { get; set; }
		/// <summary>
		/// 深さの確からしさフラグ
		/// </summary>
		public int DepthAccuracy { get; set; }
		/// <summary>
		/// マグニチュードの確からしさフラグ
		/// </summary>
		public int MagnitudeAccuracy { get; set; }

		/// <summary>
		/// 警報コード一覧
		/// </summary>
		public List<int> WarningAreas { get; set; } = new();
	}
}
