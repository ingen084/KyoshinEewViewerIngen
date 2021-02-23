using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Rendering;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KyoshinEewViewer
{
	internal class Program
	{
		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.
		public static void Main(string[] args) => BuildAvaloniaApp()
			.StartWithClassicDesktopLifetime(args);

		// Avalonia configuration, don't remove; also used by visual designer.
		public static AppBuilder BuildAvaloniaApp()
		{
			var builder = AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.LogToTrace()
				.UseSkia()
				.UseReactiveUI();

			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			{
				if (DwmIsCompositionEnabled(out bool dwmEnabled) == 0 && dwmEnabled)
				{
					var wp = builder.WindowingSubsystemInitializer;
					return builder.UseWindowingSubsystem(() =>
					{
						wp();
						AvaloniaLocator.CurrentMutable.Bind<IRenderTimer>().ToConstant(new WindowsDWMRenderTimer());
					});
				}
			}
			return builder;
		}
		[DllImport("Dwmapi.dll")]
		private static extern int DwmIsCompositionEnabled(out bool enabled);
	}

	// from https://github.com/AvaloniaUI/Avalonia/issues/2945
	class WindowsDWMRenderTimer : IRenderTimer
	{
		public event Action<TimeSpan>? Tick;
		private Thread RenderTicker { get; }
		public WindowsDWMRenderTimer()
		{
			RenderTicker = new Thread(() =>
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				while (true)
				{
					_ = DwmFlush();
					Tick?.Invoke(sw.Elapsed);
				}
			})
			{
				IsBackground = true
			};
			RenderTicker.Start();
		}
		[DllImport("Dwmapi.dll")]
		private static extern int DwmFlush();
	}
}
