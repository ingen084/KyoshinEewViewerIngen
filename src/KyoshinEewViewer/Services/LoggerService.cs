using System;
using System.Diagnostics;
using System.IO;

namespace KyoshinEewViewer.Services
{
	public class LoggerService
	{
		// TODO: Eventに移行する
		public event Action<string> WarningMessageUpdated;

		private string LogDirectory { get; set; }

		public LoggerService(ConfigurationService configService)
		{
			if (configService.Configuration.EnableLogging)
				LogDirectory = configService.Configuration.LogDirectory;
			configService.Configuration.ConfigurationUpdated += c => Trace($"Updated: {c}");
		}

		public void OnWarningMessageUpdated(string message)
			=> WarningMessageUpdated?.Invoke(message);

		[Conditional("DEBUG")]
		public void Trace(string message)
			=> WriteLog("TRCE", message);

		public void Debug(string message)
			=> WriteLog("DEBG", message);

		public void Info(string message)
			=> WriteLog("情報", message);

		public void Warning(string message)
			=> WriteLog("警告", message);

		public void Error(string message)
			=> WriteLog("エラー", message);

		// MEMO: 実はFyraで使ってるログの処理とほぼ一緒
		private void WriteLog(string type, string message)
		{
			if (string.IsNullOrWhiteSpace(LogDirectory))
				return;
			try
			{
				lock (LogDirectory)
				{
					if (!Directory.Exists(LogDirectory))
						Directory.CreateDirectory(LogDirectory);

					var fileName = "KEVi_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
					using var writer = new StreamWriter(new FileStream(Path.Combine(LogDirectory, fileName), FileMode.Append, FileAccess.Write));
					writer.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] [{type}]: {message}");
				}
			}
			catch
			{
				// 例外が発生したらログ記録は中止しておく
				LogDirectory = null;
				// ConfigService.Configuration.AlwaysUseImageParseMode = false;
			}
		}
	}
}