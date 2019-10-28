using KyoshinEewViewer.Models;
using Prism.Events;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace KyoshinEewViewer.Services
{
	public class UpdateCheckService
	{
		public VersionInfo[] AliableUpdateVersions { get; private set; }

		private ConfigurationService ConfigService { get; }

		private Timer checkUpdateTask;
		private readonly HttpClient client = new HttpClient();

		private IEventAggregator Aggregator { get; }

		private const string UpdateCheckUrl = "https://ingen084.github.io/KyoshinEewViewer/updates.json";

		public UpdateCheckService(ConfigurationService configuration, IEventAggregator aggregator)
		{
			Aggregator = aggregator;
			ConfigService = configuration ?? throw new ArgumentNullException(nameof(configuration));
			ConfigService.Configuration.ConfigurationUpdated += n =>
			{
				if (checkUpdateTask == null)
					return;
				if (ConfigService.Configuration.EnableAutoUpdateCheck &&
					(n == nameof(ConfigService.Configuration.EnableAutoUpdateCheck) || n == nameof(ConfigService.Configuration.UseUnstableBuild)))
					checkUpdateTask.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(100));
			};
		}

		public void StartUpdateCheckTask()
		{
			checkUpdateTask = new Timer(s =>
			{
				if (!ConfigService.Configuration.EnableAutoUpdateCheck)
					return;
				try
				{
					var currentVersion = Assembly.GetExecutingAssembly()?.GetName().Version;
					// 取得してでかい順に並べる
					var versions = JsonSerializer.Deserialize<VersionInfo[]>(client.GetStringAsync(UpdateCheckUrl).Result)
									.OrderByDescending(v => v.Version)
									.Where(v =>
										(ConfigService.Configuration.UseUnstableBuild ? true : v.Version.Minor == 0)
										&& v.Version > currentVersion);
					if (!versions.Any())
					{
						AliableUpdateVersions = null;
						Aggregator.GetEvent<Events.UpdateAvailableEvent>().Publish(false);
						return;
					}
					AliableUpdateVersions = versions.ToArray();
					Aggregator.GetEvent<Events.UpdateAvailableEvent>().Publish(true);
				}
				catch (Exception ex)
				{
					Debug.WriteLine("UpdateCheck Error: " + ex);
				}
			}, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(100));
		}
	}
}