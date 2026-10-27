using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using R3;
using Sentry;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace KyoshinEewViewer;

public static class LoggingAdapter
{
	/// <summary>
	/// DI コンテナへ登録するためのロガーファクトリ
	/// </summary>
	public static ILoggerFactory? Factory { get; private set; }
	/// <summary>
	/// デバッグウィンドウ用のメモリ内ログプロバイダ
	/// </summary>
	public static InMemoryLoggerProvider? InMemoryProvider { get; private set; }

	public static bool EnableConsoleLogger { get; set; }
	public static bool EnableDebugLog { get; set; }

	public static void Setup(KyoshinEewViewerConfiguration config)
	{
		// メモリ内ログプロバイダーを作成
		InMemoryProvider = new InMemoryLoggerProvider(maxLogCount: 1000);

		var sendCrashReport = !RuntimeInformation.RuntimeIdentifier.Contains("wasm") && config.Update.SendCrashReport;
		Factory = LoggerFactory.Create(builder =>
		{
			builder.AddProvider(InMemoryProvider);

			if (EnableDebugLog)
				builder.SetMinimumLevel(LogLevel.Debug);
#if DEBUG
			builder.SetMinimumLevel(LogLevel.Debug).AddDebug();
#endif
			if (sendCrashReport)
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
					// メモリ不足の原因を調べられるよう、プロセスの資源の使用量を添付する
					o.SetBeforeSend((e, _) => ProcessResourceRecorder.Attach(e));
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
						// 権限が存在しない場合、書き込めなかった場所とは別のディレクトリにフォールバック
						// スタートアップ登録から起動した場合などは、カレントディレクトリにも書き込めないことがある
						if (e.ErrorException is UnauthorizedAccessException)
						{
							var failedPath = Path.GetDirectoryName(e.LogFileName) ?? fullPath;
							var fallbackPath = IsSamePath(failedPath, PlatformDirectories.Logs)
								? Path.Combine(Environment.CurrentDirectory, config.Logging.Directory)
								: PlatformDirectories.Logs;
							// 書き込めなかった場所を再び指定すると、開けずに例外となり起動できなくなる
							if (IsSamePath(failedPath, fallbackPath))
								return;
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
		AppLog.SetFactory(Factory);
		if (sendCrashReport)
			ProcessResourceRecorder.Start();

		// R3 は購読ハンドラ内の例外を購読元へ伝播させず、未処理例外ハンドラへ渡して購読を継続する。
		// 既定では握り潰されるため、ログに残るよう接続しておく
		ObservableSystem.RegisterUnhandledExceptionHandler(ex
			=> AppLog.Default.LogWarning(ex, "Rx の購読処理内で例外が発生しました"));
	}

	private static bool IsSamePath(string a, string b)
		=> string.Equals(
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
			StringComparison.OrdinalIgnoreCase);
}
