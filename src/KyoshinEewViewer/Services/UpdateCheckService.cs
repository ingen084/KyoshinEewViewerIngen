using DynamicData.Binding;
using KyoshinEewViewer.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

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
		//"https://jenkins.ingen084.net/job/KyoshinEewViewerIngen/job/refactor_avalonia/lastSuccessfulBuild/api/json";

		public UpdateCheckService()
		{
			Logger = LoggingService.CreateLogger(this);
			Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEVi;" + Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
			CheckUpdateTask = new Timer(async s =>
			{
				if (!ConfigurationService.Default.Update.Enable)
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
										(ConfigurationService.Default.Update.UseUnstableBuild || v?.Version?.Build == 0)
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
			ConfigurationService.Default.Update.WhenValueChanged(x => x.Enable).Subscribe(x => CheckUpdateTask.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(100)));
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
