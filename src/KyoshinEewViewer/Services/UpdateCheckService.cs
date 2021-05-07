using DynamicData.Binding;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class UpdateCheckService
	{
		private static UpdateCheckService? _default;
		public static UpdateCheckService Default => _default ??= new UpdateCheckService();

		public VersionInfo[]? AliableUpdateVersions { get; private set; }

		private Timer? CheckUpdateTask { get; set; }
		private HttpClient Client { get; } = new HttpClient();

		private ILogger Logger { get; }


		private const string UpdateCheckUrl = "https://ingen084.github.io/KyoshinEewViewer/updates.json";

		public UpdateCheckService()
		{
			Logger = LoggingService.CreateLogger(this);
			Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
			ConfigurationService.Default.Update.WhenValueChanged(x => x.Enable).Subscribe(x => CheckUpdateTask?.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(100)));
		}

		public void StartUpdateCheckTask()
		{
			CheckUpdateTask = new Timer(async s =>
			{
				if (!ConfigurationService.Default.Update.Enable)
					return;
				try
				{
					var currentVersion = Assembly.GetExecutingAssembly()?.GetName().Version;
					// 取得してでかい順に並べる
					var versions = JsonSerializer.Deserialize<VersionInfo[]>(await Client.GetStringAsync(UpdateCheckUrl))
									?.OrderByDescending(v => v.Version)
									.Where(v =>
										(ConfigurationService.Default.Update.UseUnstableBuild || v?.Version?.Minor == 0)
										&& v.Version > currentVersion);
					if (!versions?.Any() ?? true)
					{
						MessageBus.Current.SendMessage(new UpdateFound(AliableUpdateVersions = null));
						return;
					}
					MessageBus.Current.SendMessage(new UpdateFound(AliableUpdateVersions = versions?.ToArray()));
				}
				catch (Exception ex)
				{
					Logger.LogWarning("UpdateCheck Error: " + ex);
				}
			}, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(100));
		}
	}
}
