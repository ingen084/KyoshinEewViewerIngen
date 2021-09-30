using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Runtime;

namespace KyoshinEewViewer
{
	internal class Program
	{
		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.
		public static void Main(string[] args)
		{
#if !DEBUG
			// 例外処理
			AppDomain.CurrentDomain.UnhandledException += (o, e) =>
			{
				System.IO.File.WriteAllText($"KEVi_Crash_Domain_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt", e.ExceptionObject.ToString());
				Environment.Exit(-1);
			};
#endif
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
			.UseReactiveUI();
	}
}
