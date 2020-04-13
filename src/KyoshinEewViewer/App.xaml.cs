using KyoshinEewViewer.Extensions;
using KyoshinEewViewer.Models.Events;
using KyoshinEewViewer.Views;
using Prism.Events;
using Prism.Ioc;
using Prism.Unity;
using System.Diagnostics;
using System.Windows;
using Unity;

namespace KyoshinEewViewer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		private ApplicationClosing ClosingEvent { get; set; }

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

#if !DEBUG
			// 例外処理
			AppDomain.CurrentDomain.UnhandledException += (o, e) =>
			{
				File.WriteAllText($"KEVi_Crash_Domain_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt", e.ExceptionObject.ToString());
				MessageBox.Show("エラーが発生しました。\n続けて発生する場合は作者までご連絡ください。", "クラッシュしました…", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(-1);
			};

			// 例外処理
			DispatcherUnhandledException += (o, e) =>
			{
				File.WriteAllText($"KEVi_Crash_Dispatcher_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt", e.Exception.ToString());
				MessageBox.Show("エラーが発生しました。\n続けて発生する場合は作者までご連絡ください。", "クラッシュしました！", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(-1);
			};
#else
			PresentationTraceSources.Refresh();
			PresentationTraceSources.DataBindingSource.Listeners.Add(new ConsoleTraceListener());
			PresentationTraceSources.DataBindingSource.Listeners.Add(new DebugTraceListener());
			PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning | SourceLevels.Error;
#endif
		}
#if DEBUG
		private class DebugTraceListener : TraceListener
		{
			public override void Write(string message)
			{
			}

			public override void WriteLine(string message)
			{
				Debugger.Break();
			}
		}
#endif

		protected override Window CreateShell()
		{
			var aggregator = Container.Resolve<IEventAggregator>();
			ClosingEvent = aggregator.GetEvent<ApplicationClosing>();
			return Container.Resolve<MainWindow>();
		}

		protected override void RegisterTypes(IContainerRegistry containerRegistry)
		{
			var container = containerRegistry.GetContainer();

			container.RegisterInstanceAndResolve<Services.NotifyIconService>();
			container.RegisterInstanceAndResolve<Services.ThemeService>();

			container.RegisterSingleton<Services.ConfigurationService>();
			container.RegisterSingleton<Services.TimerService>();
			container.RegisterSingleton<Services.LoggerService>();
			container.RegisterSingleton<Services.KyoshinMonitorWatchService>();
			container.RegisterSingleton<Services.LoggerService>();
			container.RegisterSingleton<Services.TrTimeTableService>();
			container.RegisterSingleton<Services.UpdateCheckService>();

			containerRegistry.RegisterInstance(Dispatcher);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			ClosingEvent?.Publish();
		}
	}
}