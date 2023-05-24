using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using ReactiveUI;
using Splat;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static KyoshinEewViewer.NativeMethods;

namespace KyoshinEewViewer;

public class App : Application
{
	public static ThemeSelector? Selector { get; private set; }
	public static Window? MainWindow { get; set; }

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		// フォントリソースのURLメモ
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Regular.otf"
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Bold.otf"
		// "avares://KyoshinEewViewer.Core/Assets/Fonts/FontAwesome6Free-Solid-900.otf"
		// "avares://FluentAvalonia/Fonts/FluentAvalonia.ttf"

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);

			var splashWindow = new SplashWindow();
			splashWindow.Show();

			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			var subWindow = Locator.Current.RequireService<SubWindowsService>();

			// クラッシュファイルのダンプ･再起動設定
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				WerRegisterAppLocalDump("./Dumps");
				RegisterApplicationRestart($"-c \"{Environment.CurrentDirectory.Replace("\"", "\\\"")}\" {(StartupOptions.Current?.StandaloneSeriesName is { } ssn ? $"-s {ssn.Replace("\"", "\\\"")}" : "")}", RestartFlags.None);
			}

			Selector.ApplyTheme(config.Theme.WindowThemeName, config.Theme.IntensityThemeName);
			Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
				.Subscribe(x =>
				{
					config.Theme.IntensityThemeName = x?.Name ?? "Standard";
					FixedObjectRenderer.UpdateIntensityPaintCache(desktop.Windows[0]);
				});

			Task.Run(async () =>
			{
				// 多重起動警告
				if (StartupOptions.Current?.StandaloneSeriesName is null && Process.GetProcessesByName("KyoshinEewViewer").Count(p => p.Responding) > 1)
				{
					var mre = new ManualResetEventSlim(false);
					DuplicateInstanceWarningWindow? dialog = null;
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						dialog = new DuplicateInstanceWarningWindow();
						dialog.Closed += (s, e) => mre.Set();
						dialog.Show(splashWindow);
					});
					mre.Wait();
					if (!dialog?.IsContinue ?? false)
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							splashWindow.Close();
							desktop.Shutdown();
						});
						return;
					}
				}

				// ウィザード表示
				if (
					config.ShowWizard &&
					StartupOptions.Current?.StandaloneSeriesName is null
				)
				{
					await Dispatcher.UIThread.InvokeAsync(async () =>
					{
						await subWindow.ShowDialogSetupWizardWindow(async () =>
						{
							await Task.Delay(500);
							await Dispatcher.UIThread.InvokeAsync(() =>
							{
								splashWindow?.Close();
								splashWindow = null;
							});
						});
					});
					config.ShowWizard = false;
					ConfigurationLoader.Save(config);
				}

				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					desktop.MainWindow = MainWindow = new MainWindow
					{
						DataContext = Locator.Current.RequireService<MainWindowViewModel>(),
					};
					Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
					{
						config.Theme.WindowThemeName = x?.Name ?? "Light";
						FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow);

						if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || desktop.MainWindow.PlatformImpl?.TryGetFeature<IPlatformNativeSurfaceHandle>()?.Handle is not { } handle)
							return;
						// Windowsにおけるウィンドウ周囲の色変更
						Avalonia.Media.Color FindColorResource(string name)
							=> (Avalonia.Media.Color)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
						bool FindBoolResource(string name)
							=> (bool)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

						var isDarkTheme = FindBoolResource("IsDarkTheme");
						var useDarkMode = isDarkTheme ? 1 : 0;
						DwmSetWindowAttribute(
							handle,
							Dwmwindowattribute.DwmwaUseImmersiveDarkMode,
							ref useDarkMode,
							Marshal.SizeOf(useDarkMode));

						var color = FindColorResource("TitleBackgroundColor");
						var intColor = color.R | color.G << 8 | color.B << 16;
						DwmSetWindowAttribute(
							handle,
							Dwmwindowattribute.DwmwaCaptionColor,
							ref intColor,
							Marshal.SizeOf(intColor));
					});
					MainWindow.Opened += async (s, e) =>
					{
						await Task.Delay(1000);
						subWindow.SetupWizardWindow?.Close();
						splashWindow?.Close();
						splashWindow = null;
					};
					MainWindow.Show();
					MainWindow.Activate();
				});
			});

			desktop.Exit += (s, e) =>
			{
				MessageBus.Current.SendMessage(new ApplicationClosing());
				ConfigurationLoader.Save(config);
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// override RegisterServices register custom service
	/// </summary>
	public override void RegisterServices()
	{
		Locator.CurrentMutable.RegisterLazySingleton(ConfigurationLoader.Load, typeof(KyoshinEewViewerConfiguration));
		Locator.CurrentMutable.RegisterLazySingleton(() => new SeriesController(), typeof(SeriesController));
		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		if (!Design.IsDesignMode)
		{
			var timer = AvaloniaLocator.CurrentMutable.GetRequiredService<IRenderTimer>();
			AvaloniaLocator.CurrentMutable.Bind<IRenderTimer>().ToConstant(new FrameSkippableRenderTimer(timer, config));
		}
		LoggingAdapter.Setup(config);

		SetupIoc(Locator.GetLocator());
		base.RegisterServices();
	}

	public void OpenSettingsClicked(object sender, EventArgs args)
		=> MessageBus.Current.SendMessage(new ShowSettingWindowRequested());

	public static void SetupIoc(IDependencyResolver resolver)
		=> SplatRegistrations.SetupIOC(resolver);
}

public class FrameSkippableRenderTimer : IRenderTimer
{
	private IRenderTimer ParentTimer { get; }
	private ulong FrameCount { get; set; }

	public void NotClientImplementable() => throw new NotImplementedException();

	public bool RunsInBackground => ParentTimer.RunsInBackground;

	public event Action<TimeSpan>? Tick;

	public FrameSkippableRenderTimer(IRenderTimer parentTimer, KyoshinEewViewerConfiguration config)
	{
		ParentTimer = parentTimer;

		// ここに流れた時点ですでに RenderLoop のハンドラーが設定されているのでリフレクションで無理やり奪う
		var tickEvent = parentTimer.GetType().GetField("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
		if (tickEvent?.GetValue(parentTimer) is MulticastDelegate handler)
		{
			foreach (var d in handler.GetInvocationList().Cast<Action<TimeSpan>>())
			{
				ParentTimer.Tick -= d;
				Tick += d;
			}
		}

		ParentTimer.Tick += t =>
		{
			if (config.FrameSkip <= 1 || FrameCount++ % config.FrameSkip == 0)
				Tick?.Invoke(t);
		};
	}
}
