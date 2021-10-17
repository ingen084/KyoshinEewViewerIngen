using DynamicData.Binding;
using KyoshinEewViewer.Core.Models;
using Microsoft.Extensions.Logging;
using Mono.Unix;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class UpdateCheckService
	{
		private static UpdateCheckService? _default;
		public static UpdateCheckService Default => _default ??= new UpdateCheckService();

		public VersionInfo[]? AvailableUpdateVersions { get; private set; }

		private Timer CheckUpdateTask { get; }
		private HttpClient Client { get; } = new HttpClient();

		private ILogger Logger { get; }

		public event Action<VersionInfo[]?>? Updated;


		private const string UpdateCheckUrl = "https://svs.ingen084.net/kyoshineewviewer/updates.json";
		private const string UpdatersCheckUrl = "https://svs.ingen084.net/kyoshineewviewer/updaters.json";
		//"https://jenkins.ingen084.net/job/KyoshinEewViewerIngen/job/refactor_avalonia/lastSuccessfulBuild/api/json";

		public UpdateCheckService()
		{
			Logger = LoggingService.CreateLogger(this);
			Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEVi;" + Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
			CheckUpdateTask = new Timer(async s =>
			{
				if (!ConfigurationService.Current.Update.Enable)
					return;

				try
				{
					var currentVersion = Assembly.GetExecutingAssembly()?.GetName().Version;

					// α版専用処理
					//var info = JsonSerializer.Deserialize<JenkinsBuildInformation>(await Client.GetStringAsync(UpdateCheckUrl));
					//if (info?.Number > currentVersion?.Revision)
					//{
					//	MessageBus.Current.SendMessage(new UpdateFound(AvailableUpdateVersions = new[]
					//	{
					//		new VersionInfo
					//		{
					//			Time = DateTime.Now,
					//			VersionString = "0.9." + (info?.Number ?? 0) + ".0",
					//			Message = "新しいα版のビルドが公開されています。\nビルド#" + (info?.Number ?? 0) + "\n※このメッセージは自動更新のため更新内容がない場合でも表示されることがあります。",
					//		}
					//	}));
					//	return;
					//}
					//MessageBus.Current.SendMessage(new UpdateFound(AvailableUpdateVersions = null));

					// 取得してでかい順に並べる
					var versions = JsonSerializer.Deserialize<VersionInfo[]>(await Client.GetStringAsync(UpdateCheckUrl))
									?.OrderByDescending(v => v.Version)
									.Where(v =>
										(ConfigurationService.Current.Update.UseUnstableBuild || v?.Version?.Build == 0)
										&& v.Version > currentVersion);
					if (!versions?.Any() ?? true)
					{
						Updated?.Invoke(AvailableUpdateVersions = null);
						return;
					}
					Updated?.Invoke(AvailableUpdateVersions = versions?.ToArray());
				}
				catch (Exception ex)
				{
					Logger.LogWarning("UpdateCheck Error: {ex}", ex);
				}
			}, null, Timeout.Infinite, Timeout.Infinite);
			ConfigurationService.Current.Update.WhenValueChanged(x => x.Enable).Subscribe(x => CheckUpdateTask.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(100)));
		}

		private bool IsUpdating { get; set; } = false;
		/// <summary>
		/// アップデーターのプロセスを開始する
		/// </summary>
		public async Task StartUpdater()
		{
			if (IsUpdating)
				return;
			Logger.LogInformation("アップデータのセットアップを開始します");

			IsUpdating = true;
			try
			{
				var store = JsonSerializer.Deserialize<Dictionary<string, string>>(await Client.GetStringAsync(UpdatersCheckUrl));
				if (store == null)
					throw new Exception("ストアをパースできません");
				var ri = RuntimeInformation.RuntimeIdentifier;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					ri = "linux-x64";
				if (!store.ContainsKey(ri))
					throw new Exception($"ストアに現在の環境 {ri} がありません");

				var fileName = Path.GetTempFileName();
				Logger.LogInformation("アップデータをダウンロードしています: {from} -> {to}", store[ri], fileName);
				using (var file = File.OpenWrite(fileName))
					await (await Client.GetStreamAsync(store[ri])).CopyToAsync(file);

				if (!Directory.Exists("Updater"))
					Directory.CreateDirectory("Updater");

				Logger.LogInformation("アップデータを展開しています");
				await Task.Run(() => ZipFile.ExtractToDirectory(fileName, "Updater", true));
				File.Delete(fileName);

				// Windowsでない場合実行権限を付与
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					new UnixFileInfo("Updater/KyoshinEewViewer.Updater").FileAccessPermissions =
						FileAccessPermissions.UserExecute | FileAccessPermissions.UserRead | FileAccessPermissions.UserWrite |
						FileAccessPermissions.GroupExecute | FileAccessPermissions.GroupRead | FileAccessPermissions.OtherRead;

				// 現在の設定を保存
				ConfigurationService.Save();
				// プロセスを起動
				Process.Start(new ProcessStartInfo(Path.Combine("./Updater", "KyoshinEewViewer.Updater")) { WorkingDirectory = "./Updater" });
				// 自身は終了
				App.MainWindow?.Close();
			}
			catch (Exception ex)
			{
				Logger.LogError("アップデータの起動に失敗しました {ex}", ex);
			}
			finally
			{
				IsUpdating = false;
			}
		}

		public void StartUpdateCheckTask()
			=> CheckUpdateTask.Change(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(100));
	}

	public class JenkinsBuildInformation
	{
		[JsonPropertyName("number")]
		public int Number { get; set; }
	}
}
