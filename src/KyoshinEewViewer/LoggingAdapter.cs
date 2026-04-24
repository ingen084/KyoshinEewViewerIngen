using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using Sentry;
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
	private static InMemoryLoggerProvider? InMemoryProvider { get; set; }

	public static bool EnableConsoleLogger { get; set; }
	public static bool EnableDebugLog { get; set; }

	public static void Setup(KyoshinEewViewerConfiguration config)
	{
		// メモリ内ログプロバイダーを作成
		InMemoryProvider = new InMemoryLoggerProvider(maxLogCount: 1000);

		// Splatに登録
		Locator.CurrentMutable.RegisterConstant(InMemoryProvider);

		Factory = LoggerFactory.Create(builder =>
		{
			builder.AddProvider(InMemoryProvider);

			if (EnableDebugLog)
				builder.SetMinimumLevel(LogLevel.Debug);
#if DEBUG
			builder.SetMinimumLevel(LogLevel.Debug).AddDebug();
#endif
			if (!RuntimeInformation.RuntimeIdentifier.Contains("wasm") && config.Update.SendCrashReport)
				builder.AddSentry(o =>
				{
					o.Dsn = "https://565aa07785854f1aabdaac930c1a483f@sentry.ingen084.net/2";
					o.TracesSampleRate = 0.01; // 1% 送信する
					o.IsGlobalModeEnabled = true;
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
						s.User = new SentryUser {
							Id = config.InstanceId.ToString(),
							IpAddress = "{{auto}}",
						};
					});
				});

			if (EnableConsoleLogger)
				builder.AddConsole();

			if (!config.Logging.Enable)
				return;
			try
			{
				// macOSではログディレクトリを固定、その他のプラットフォームでは設定値を使用
				string fullPath;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					fullPath = PlatformDirectories.Logs;
				}
				else if (config.Logging.UseCurrentDirectory)
				{
					// 実行ディレクトリを使用
					fullPath = Path.IsPathRooted(config.Logging.Directory)
						? config.Logging.Directory
						: Path.Combine(Environment.CurrentDirectory, config.Logging.Directory);
				}
				else
				{
					// アプリケーションデータディレクトリを使用
					fullPath = Path.IsPathRooted(config.Logging.Directory)
						? config.Logging.Directory
						: Path.Combine(PlatformDirectories.ApplicationData, config.Logging.Directory);
				}

				PlatformDirectories.EnsureDirectoryExists(fullPath);

				builder.AddFile(Path.Combine(fullPath, "KEVi_{0:yyyy}-{0:MM}-{0:dd}.log"), fileLoggerOpts =>
				{
					fileLoggerOpts.FormatLogFileName = fName => string.Format(fName, DateTime.Now);
					fileLoggerOpts.HandleFileError = e =>
					{
						// 権限が存在しない場合、カレントディレクトリにフォールバック
						if (e.ErrorException is UnauthorizedAccessException)
						{
							var fallbackPath = Path.Combine(Environment.CurrentDirectory, config.Logging.Directory);
							try
							{
								if (!Directory.Exists(fallbackPath))
									Directory.CreateDirectory(fallbackPath);
								e.UseNewLogFileName(Path.Combine(fallbackPath, "KEVi_{0:yyyy}-{0:MM}-{0:dd}.log"));
							}
							catch
							{
								// フォールバックも失敗した場合はログを無効化
							}
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
