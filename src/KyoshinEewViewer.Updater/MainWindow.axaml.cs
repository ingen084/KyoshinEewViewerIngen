using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Core.Models;
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
using System.Threading.Tasks;

namespace KyoshinEewViewer.Updater
{
	public class MainWindow : Window
	{
		private HttpClient Client { get; } = new();
		private const string UpdateCheckUrl = "https://svs.ingen084.net/kyoshineewviewer/updates.json";
		private const string UpdateDirectory = "../";
		private const string SettingsFileName = "config.json";

		public MainWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
			Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEViUpdater;" + Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
			this.FindControl<Button>("closeButton").Tapped += (s, e) => Close();
			DoUpdate();
		}

		private async void DoUpdate()
		{
			var progress = this.FindControl<ProgressBar>("progress");
			progress.Value = 0;
			progress.IsIndeterminate = true;
			var progressText = this.FindControl<TextBlock>("progressText");
			progressText.Text = "";
			var infoText = this.FindControl<TextBlock>("infoText");
			var closeButton = this.FindControl<Button>("closeButton");

			try
			{
				while (Process.GetProcessesByName("KyoshinEewViewer").Any())
				{
					closeButton.IsEnabled = true;
					infoText.Text = "KyoshinEewViewer のプロセスが終了するのを待っています";
					await Task.Delay(1000);
				}
				closeButton.IsEnabled = false;
				infoText.Text = "適用可能な更新を取得しています";

				// アプリによる保存を待ってから
				await Task.Delay(1000);
				if (!File.Exists(Path.Combine(UpdateDirectory, SettingsFileName)))
					throw new Exception("KyoshinEewViewerが見つかりません");
				if (JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(Path.Combine(UpdateDirectory, SettingsFileName))) is not KyoshinEewViewerConfiguration config)
					throw new Exception("KyoshinEewViewerの設定ファイルを読み込むことができません");

				var version = JsonSerializer.Deserialize<VersionInfo[]>(await Client.GetStringAsync(UpdateCheckUrl))
					?.OrderByDescending(v => v.Version).Where(v => v.Version > config.SavedVersion).FirstOrDefault();

				if (string.IsNullOrWhiteSpace(version?.Url))
				{
					infoText.Text = "適用可能な更新はありません";
					progress.IsIndeterminate = false;
					closeButton.IsEnabled = true;
					return;
				}

				infoText.Text = $"バージョン {version.Version} に更新を行います";

				var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(await Client.GetStringAsync(version.Url));
				if (catalog == null)
					throw new Exception("アップデートカタログを取得できません");

				if (!catalog.ContainsKey(RuntimeInformation.RuntimeIdentifier))
				{
					infoText.Text = "現在のプラットフォームで自動更新は利用できません";
					progress.IsIndeterminate = false;
					closeButton.IsEnabled = true;
					return;
				}

				infoText.Text = $"バージョン {version.Version} をダウンロードしています";
				progress.IsIndeterminate = false;

				var tmpFileName = Path.GetTempFileName();
				// ダウンロード開始
				using (var fileStream = File.OpenWrite(tmpFileName))
				{
					using var response = await Client.GetAsync(catalog[RuntimeInformation.RuntimeIdentifier], HttpCompletionOption.ResponseHeadersRead);
					progress.Maximum = response.Content.Headers.ContentLength ?? throw new Exception("DLサイズが取得できません");

					using var inputStream = await response.Content.ReadAsStreamAsync();

					var total = 0;
					var buffer = new byte[1024];
					while (true)
					{
						var readed = await inputStream.ReadAsync(buffer);
						if (readed == 0)
							break;

						progress.Value = total += readed;
						progressText.Text = $"ダウンロード中: {progress.Value / progress.Maximum * 100:0.00}%";

						await fileStream.WriteAsync(buffer, 0, readed);
					}
				}

				infoText.Text = "ファイルを展開しています";
				progress.IsIndeterminate = true;
				progressText.Text = "";

				await Task.Run(() => ZipFile.ExtractToDirectory(tmpFileName, UpdateDirectory, true));
				File.Delete(tmpFileName);
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					new UnixFileInfo(Path.Combine(UpdateDirectory, "KyoshinEewViewer")).FileAccessPermissions =
						FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherExecute;

				infoText.Text = "更新が完了しました アプリケーションを起動しています";
				progress.IsIndeterminate = false;

				await Task.Delay(100);

				Process.Start(new ProcessStartInfo(Path.Combine(UpdateDirectory, "KyoshinEewViewer")) { WorkingDirectory = UpdateDirectory });

				await Task.Delay(2000);

				Close();
			}
			catch (Exception ex)
			{
				infoText.Text = "更新中に問題が発生しました";
				progressText.Text = ex.Message;
				progress.IsIndeterminate = false;
				closeButton.IsEnabled = true;
			}
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
	}
}
