using Avalonia;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace KyoshinEewViewer.Updater;

static class Program
{
	public static string? OverrideKevPath { get; private set; }
	public static void Main(string[] args)
	{
		if (args.Length >= 1)
			OverrideKevPath = args[0];
		// 2つ目の引数がRunAsであれば管理者権限で再起動を試みる
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && args.Length == 2 && args[1] == "run-as")
		{
			var proc = new ProcessStartInfo()
			{
				WorkingDirectory = Environment.CurrentDirectory,
				FileName = Assembly.GetExecutingAssembly().Location,
				Verb = "RunAs",
			};
			proc.ArgumentList.Add(args[0]);
			Process.Start(proc);
			Thread.Sleep(2000);
			return;
		}
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
}
