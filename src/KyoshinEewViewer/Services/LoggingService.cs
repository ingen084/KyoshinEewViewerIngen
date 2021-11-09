using Microsoft.Extensions.Logging;
using System;
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

				if (!ConfigurationService.Current.Logging.Enable)
				return;

			var fullPath = ConfigurationService.Current.Logging.Directory;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !fullPath.StartsWith("/"))
				fullPath = Path.Combine("~/.kevi", fullPath);

			if (!Directory.Exists(fullPath))
				Directory.CreateDirectory(fullPath);
			builder.AddFile(Path.Combine(fullPath, "KEVi_{0:yyyy}-{0:MM}-{0:dd}.log"), fileLoggerOpts =>
			{
				fileLoggerOpts.FormatLogFileName = fName => string.Format(fName, DateTime.Now);
			});
		});
	}

	public static ILogger<T> CreateLogger<T>()
		=> Default.Factory.CreateLogger<T>();
	public static ILogger<T> CreateLogger<T>(T _)
		=> Default.Factory.CreateLogger<T>();
}
