using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;
public class EventHookService
{
	protected EventHookService() { }

	public static async Task Run(string eventName, Dictionary<string, string> parameters)
	{
		if (!ConfigurationService.Current.EventHook.Enabled)
			return;
		if (!Directory.Exists(ConfigurationService.Current.EventHook.FolderPath))
			return;
		try
		{
			var files = Directory.GetFiles(ConfigurationService.Current.EventHook.FolderPath);
			foreach (var file in files)
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					var info = new ProcessStartInfo("cmd", $"/c start /b {file.Replace("&", "^&")}")
					{
						WorkingDirectory = Path.GetDirectoryName(file),
						CreateNoWindow = true,
					};
					info.EnvironmentVariables["KEVI_EVENT_TYPE"] = eventName;
					foreach (var kvp in parameters)
						info.EnvironmentVariables[kvp.Key] = kvp.Value;
					var proc = Process.Start(info);
					if (proc != null)
						await proc.WaitForExitAsync();
				}
				else
				{
					// 実行権限をチェック
					var check = Process.Start(new ProcessStartInfo("bash", $"-c \"if [ -x {file.Replace("\"", "\\\"")} ]; then exit 0; else exit 1; fi\"")
					{
						WorkingDirectory = Path.GetDirectoryName(file),
						CreateNoWindow = true,
					});
					if (check == null)
						continue;

					await check.WaitForExitAsync();
					if (check.ExitCode != 0)
						continue;

					// 実行できそうであればそのまま実行
					var info = new ProcessStartInfo(file)
					{
						WorkingDirectory = Path.GetDirectoryName(file),
						CreateNoWindow = true,
					};
					info.EnvironmentVariables["KEVI_EVENT_TYPE"] = eventName;
					foreach (var kvp in parameters)
						info.EnvironmentVariables[kvp.Key] = kvp.Value;
					var proc = Process.Start(info);
					if (proc != null)
						await proc.WaitForExitAsync();
				}
			}
		}
		catch (Exception ex)
		{
			LoggingService.CreateLogger<EventHookService>().LogWarning("イベントフックの実行に失敗: {ex}", ex);
		}
	}
}
