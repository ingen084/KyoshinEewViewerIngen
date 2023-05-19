using KyoshinEewViewer.Core.Models;
using Microsoft.Extensions.Logging;
using Splat;
using Splat.Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace KyoshinEewViewer;

public static class LoggingAdapter
{
	private static ILoggerFactory? Factory { get; set; }

	public static bool EnableConsoleLogger { get; set; }

	public static void Setup(KyoshinEewViewerConfiguration config)
	{
		Factory = LoggerFactory.Create(builder =>
		{
#if DEBUG
			builder.SetMinimumLevel(LogLevel.Debug).AddDebug();
#endif
			if (config.Update.SendCrashReport)
				builder.AddSentry(o =>
				{
					o.Dsn = "https://565aa07785854f1aabdaac930c1a483f@sentry.ingen084.net/2";
					o.TracesSampleRate = 0.01; // 1% 送信する
					o.IsGlobalModeEnabled = false;
#if DEBUG
					o.Environment = "development";
					o.Debug = true;
#endif
					o.Release = Core.Utils.Version;
					o.AutoSessionTracking = true;
					o.MinimumBreadcrumbLevel = LogLevel.Information;
					o.MinimumEventLevel = LogLevel.Error;
					o.ConfigureScope(s =>
					{
						s.User = new()
						{
							IpAddress = "{{auto}}",
						};
					});
				});

			if (EnableConsoleLogger)
				builder.SetMinimumLevel(LogLevel.Debug).AddConsole();

			if (!config.Logging.Enable)
				return;
			try
			{
				var fullPath = config.Logging.Directory;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !fullPath.StartsWith("/"))
					fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kevi", fullPath);

				if (!Directory.Exists(fullPath))
					Directory.CreateDirectory(fullPath);
				builder.AddFile(Path.Combine(fullPath, "KEVi_{0:yyyy}-{0:MM}-{0:dd}.log"), fileLoggerOpts =>
				{
					// なんとかしてエラー回避する
					fileLoggerOpts.FormatLogFileName = fName => string.Format(fName, DateTime.Now);
					fileLoggerOpts.HandleFileError = e =>
					{
						// 権限が存在しない場合
						if (e.ErrorException is UnauthorizedAccessException)
						{
							fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kevi", config.Logging.Directory);
							e.UseNewLogFileName(Path.Combine(fullPath, "KEVi_{0:yyyy}-{0:MM}-{0:dd}.log"));
							return;
						}
						// ログファイルのオープンに失敗した場合
						if (e.ErrorException is IOException)
							e.UseNewLogFileName(Path.Combine(fullPath, $"KEVi_{{0:yyyy}}-{{0:MM}}-{{0:dd}}-{new Random().Next()}.log"));
					};
				});
			}
			catch (Exception ex)
			{
				Trace.WriteLine("ファイルロガーの作成に失敗: " + ex);
				config.Logging.Enable = false;
			}
		});
		Locator.CurrentMutable.UseMicrosoftExtensionsLoggingWithWrappingFullLogger(Factory);
	}
}
