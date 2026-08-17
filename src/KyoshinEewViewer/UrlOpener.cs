using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer;

public static class UrlOpener
{
	public static void OpenUrl(string url)
	{
		try
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				url = url.Replace("&", "^&");
				Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				Process.Start("xdg-open", url);
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				Process.Start("open", url);
		}
		catch(Exception ex)
		{
			AppLog.Default.LogError(ex, "URLオープンに失敗しました");
		}
	}
}
