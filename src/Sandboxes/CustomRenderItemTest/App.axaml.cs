using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using CustomRenderItemTest.ViewModels;
using CustomRenderItemTest.Views;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace CustomRenderItemTest;

public class App : Application
{
	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr, ref int attrValue, int attrSize);
	private enum DWMWINDOWATTRIBUTE
	{
		DWMWA_NCRENDERING_ENABLED,
		DWMWA_NCRENDERING_POLICY,
		DWMWA_TRANSITIONS_FORCEDISABLED,
		DWMWA_ALLOW_NCPAINT,
		DWMWA_CAPTION_BUTTON_BOUNDS,
		DWMWA_NONCLIENT_RTL_LAYOUT,
		DWMWA_FORCE_ICONIC_REPRESENTATION,
		DWMWA_FLIP3D_POLICY,
		DWMWA_EXTENDED_FRAME_BOUNDS,
		DWMWA_HAS_ICONIC_BITMAP,
		DWMWA_DISALLOW_PEEK,
		DWMWA_EXCLUDED_FROM_PEEK,
		DWMWA_CLOAK,
		DWMWA_CLOAKED,
		DWMWA_FREEZE_REPRESENTATION,
		DWMWA_PASSIVE_UPDATE_MODE,
		DWMWA_USE_HOSTBACKDROPBRUSH,
		DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
		DWMWA_WINDOW_CORNER_PREFERENCE = 33,
		DWMWA_BORDER_COLOR,
		DWMWA_CAPTION_COLOR,
		DWMWA_TEXT_COLOR,
		DWMWA_VISIBLE_FRAME_BORDER_THICKNESS,
		DWMWA_LAST
	};

	public static ThemeSelector? Selector { get; private set; }

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var splashWindow = new SplashWindow();
			splashWindow.Show();

			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);

			Dispatcher.UIThread.InvokeAsync(() =>
			{
				desktop.MainWindow = new MainWindow
				{
					DataContext = new MainWindowViewModel(),
				};
				Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
					.Subscribe(x => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow));
				Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
				{
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && desktop.MainWindow.PlatformImpl is not null)
					{
						Avalonia.Media.Color FindColorResource(string name)
							=> (Avalonia.Media.Color)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"マップリソース {name} が見つかりませんでした"));
						bool FindBoolResource(string name)
							=> (bool)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

						var isDarkTheme = FindBoolResource("IsDarkTheme");
						var USE_DARK_MODE = isDarkTheme ? 1 : 0;
						DwmSetWindowAttribute(
							desktop.MainWindow.PlatformImpl.Handle.Handle,
							DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
							ref USE_DARK_MODE,
							Marshal.SizeOf(USE_DARK_MODE));

						var color = FindColorResource("TitleBackgroundColor");
						var colord = color.R | color.G << 8 | color.B << 16;
						DwmSetWindowAttribute(
							desktop.MainWindow.PlatformImpl.Handle.Handle,
							DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR,
							ref colord,
							Marshal.SizeOf(colord));

						//var color2 = FindColorResource("SubForegroundColor");
						//var colord2 = color2.R | color2.G << 8 | color2.B << 16;
						//DwmSetWindowAttribute(
						//	desktop.MainWindow.PlatformImpl.Handle.Handle,
						//	DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR,
						//	ref colord2,
						//	Marshal.SizeOf(colord2));
					}
				});
				desktop.MainWindow.Show();
				desktop.MainWindow.Activate();
				splashWindow.Close();
			});

			desktop.Exit += (s, e) => MessageBus.Current.SendMessage(new ApplicationClosing());
		}
		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// override RegisterServices register custom service
	/// </summary>
	public override void RegisterServices()
	{
		var timer = AvaloniaLocator.CurrentMutable.GetService<IRenderTimer>() ?? throw new Exception("RenderTimer が取得できません");
		AvaloniaLocator.CurrentMutable.Bind<IRenderTimer>().ToConstant(new FrameSkippableRenderTimer(timer));
		AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new CustomFontManagerImpl());
		base.RegisterServices();
	}

	public class FrameSkippableRenderTimer : IRenderTimer
	{
		private IRenderTimer ParentTimer { get; }
		private ulong FrameCount { get; set; }

		public bool RunsInBackground => ParentTimer.RunsInBackground;

		public event Action<TimeSpan>? Tick;

		public FrameSkippableRenderTimer(IRenderTimer parentTimer)
		{
			ParentTimer = parentTimer;
			ParentTimer.Tick += t =>
			{
				if (FrameCount++ % 1 == 0)
					Tick?.Invoke(t);
			};
		}
	}
}
