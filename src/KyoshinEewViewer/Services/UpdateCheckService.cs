﻿using DynamicData.Binding;
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

		private Timer? CheckUpdateTask { get; set; }
		private HttpClient Client { get; } = new HttpClient();

		private ILogger Logger { get; }


		//TODO: alpha脱却時にもどす
		private const string UpdateCheckUrl = "https://jenkins.ingen084.net/job/KyoshinEewViewerIngen/job/refactor_avalonia/lastSuccessfulBuild/api/json";
		//"https://ingen084.github.io/KyoshinEewViewer/updates.json";

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

					//TODO α版専用処理
					var info = JsonSerializer.Deserialize<JenkinsBuildInformation>(await Client.GetStringAsync(UpdateCheckUrl));
					if (info?.Number > currentVersion?.Build)
					{
						MessageBus.Current.SendMessage(new UpdateFound(AvailableUpdateVersions = new[]
						{
							new VersionInfo
							{
								Time = DateTime.Now,
								VersionString = "0.9." + (info?.Number ?? 0) + ".0",
								Message = "新しいα版のビルドが公開されています。\nビルド#" + (info?.Number ?? 0) + "\n※このメッセージは自動更新のため更新内容がない場合でも表示されることがあります。",
							}
						}));
						return;
					}
					MessageBus.Current.SendMessage(new UpdateFound(AvailableUpdateVersions = null));
					return;

					// 取得してでかい順に並べる
					var versions = JsonSerializer.Deserialize<VersionInfo[]>(await Client.GetStringAsync(UpdateCheckUrl))
									?.OrderByDescending(v => v.Version)
									.Where(v =>
										(ConfigurationService.Default.Update.UseUnstableBuild || v?.Version?.Minor == 0)
										&& v.Version > currentVersion);
					if (!versions?.Any() ?? true)
					{
						MessageBus.Current.SendMessage(new UpdateFound(AvailableUpdateVersions = null));
						return;
					}
					MessageBus.Current.SendMessage(new UpdateFound(AvailableUpdateVersions = versions?.ToArray()));
				}
				catch (Exception ex)
				{
					Logger.LogWarning("UpdateCheck Error: " + ex);
				}
			}, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(100));
		}
	}

	public class JenkinsBuildInformation
	{
		[JsonPropertyName("number")]
		public int Number { get; set; }
	}
}