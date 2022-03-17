using Microsoft.Extensions.Logging;
using Sentry;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Services;

public class LoggingService
{
	private static LoggingService? _default;
	private static LoggingService Default => _default ??= new LoggingService();
	private ILoggerFactory Factory { get; }

	private LoggingService()
	{
		Factory = LoggerFactory.Create(builder =>
		{
#if DEBUG
			builder.SetMinimumLevel(LogLevel.Debug).AddDebug();
#endif
			if (ConfigurationService.Current.Update.SendCrashReport)
			{
				builder.AddSentry(o =>
				{
#if DEBUG
					o.Dsn = "https://74fd3f1d0b7a45ae9f0de10a1c98fad7@sentry.ingen084.net/4";
					o.TracesSampleRate = 1.0;
#else
					o.Dsn = "https://565aa07785854f1aabdaac930c1a483f@sentry.ingen084.net/2";
					o.TracesSampleRate = 0.03; // 3% 送信する
#endif
					o.AutoSessionTracking = true;
					o.MinimumBreadcrumbLevel = LogLevel.Information;
					o.MinimumEventLevel = LogLevel.Error;
					o.ConfigureScope(s => 
					{
						s.Release = Core.Utils.Version;
						s.User = new() 
						{
							IpAddress = "{{auto}}",
						};
					});
				});
			}
			if (!ConfigurationService.Current.Logging.Enable)
				return;

			try
			{
				var fullPath = ConfigurationService.Current.Logging.Directory;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !fullPath.StartsWith("/"))
					fullPath = Path.Combine(".kevi", fullPath);

				if (!Directory.Exists(fullPath))
					Directory.CreateDirectory(fullPath);
				builder.AddFile(Path.Combine(fullPath, "KEVi_{0:yyyy}-{0:MM}-{0:dd}.log"), fileLoggerOpts =>
				{
					fileLoggerOpts.FormatLogFileName = fName => string.Format(fName, DateTime.Now);
				});
			}
			catch (Exception ex)
			{
				Trace.WriteLine("ファイルロガーの作成に失敗: " + ex);
				ConfigurationService.Current.Logging.Enable = false;
			}
		});
	}

	public static ILogger<T> CreateLogger<T>()
		=> Default.Factory.CreateLogger<T>();
	public static ILogger<T> CreateLogger<T>(T _)
		=> Default.Factory.CreateLogger<T>();
}
