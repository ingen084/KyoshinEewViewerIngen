using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer;

internal class Program
{
	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern void MessageBox(IntPtr hWnd, string text, string caption, uint type);

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static void Main(string[] args)
	{
#if !DEBUG
		// 例外処理
		AppDomain.CurrentDomain.UnhandledException += (o, e) =>
		{
			try {
				System.IO.File.WriteAllText($"KEVi_Crash_Domain_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt", e.ExceptionObject.ToString());
				Services.LoggingService.CreateLogger<Program>().LogCritical(e.ExceptionObject as Exception, "ハンドルされていない例外が発生しました。");
			}
			catch { }
			if (Services.ConfigurationService.Current.Update.SendCrashReport)
			{
				try
				{
					using var client = new System.Net.Http.HttpClient();
					client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEVi;" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown") + ";" + RuntimeInformation.RuntimeIdentifier);
					client.Send(new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://svs.ingen084.net/kyoshineewviewer/crash.php")
					{
						Content = new System.Net.Http.StringContent(e.ExceptionObject.ToString() ?? "null"),
					});
				}
				catch { }
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var additionalMessage = "";
				if (e.ExceptionObject?.ToString()?.Contains("Dll was not found") ?? false)
					additionalMessage = "\n必要なファイルが不足しているようです。\nアプリケーションが正常に展開できているかどうかご確認ください。";
				MessageBox(IntPtr.Zero, $"クラッシュしました！{additionalMessage}\n\n==詳細==\n" + e.ExceptionObject, "KyoshinEewViewer for ingen", 0);
			}
		};
#endif
		if (args.Length == 2 && args[0] == "standalone")
		{
			StartupOptions.IsStandalone = true;
			StartupOptions.StandaloneSeriesName = args[1];
		}

		ProfileOptimization.SetProfileRoot(Environment.CurrentDirectory);
		ProfileOptimization.StartProfile("KyoshinEewViewer.jitprofile");
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
		.UsePlatformDetect()
		.LogToTrace()
		.UseSkia()
		.With(new Win32PlatformOptions
		{
			AllowEglInitialization = true,
			EnableMultitouch = true,
		})
		.With(new X11PlatformOptions
		{
			OverlayPopups = true,
		})
		.UseReactiveUI();
}
