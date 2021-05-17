using Avalonia;
using Avalonia.Rendering;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KyoshinEewViewer.Windows
{
	public static class AppBuilderExtensions
	{
		public static AppBuilder UseDwmSync(this AppBuilder builder)
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				return builder;
			if (DwmIsCompositionEnabled(out bool dwmEnabled) == 0 && dwmEnabled)
			{
				var wp = builder.WindowingSubsystemInitializer;
				return builder.UseWindowingSubsystem(() =>
				{
					wp();
					AvaloniaLocator.CurrentMutable.Bind<IRenderTimer>().ToConstant(new WindowsDWMRenderTimer());
				});
			}
			return builder;
		}

		[DllImport("Dwmapi.dll")]
		private static extern int DwmIsCompositionEnabled(out bool enabled);

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
}
