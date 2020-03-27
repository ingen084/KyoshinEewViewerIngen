using KyoshinEewViewer.Models.Events;
using KyoshinEewViewer.Views;
using Prism.Events;
using Prism.Ioc;
using System;
using System.IO;
using System.Windows;

namespace KyoshinEewViewer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		private ApplicationClosing ClosingEvent { get; set; }

#if !DEBUG
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

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
			//containerRegistry.RegisterSingleton<Services.LoggerService>();
			//containerRegistry.RegisterSingleton<Services.ConfigurationService>();
			//containerRegistry.RegisterSingleton<Services.ThemeService>();
			//containerRegistry.RegisterSingleton<Services.TrTimeTableService>();
			//containerRegistry.RegisterSingleton<Services.KyoshinMonitorWatchService>();
			//containerRegistry.RegisterSingleton<Services.UpdateCheckService>();
			//containerRegistry.RegisterSingleton<Services.TimerService>();
			//containerRegistry.RegisterSingleton<Services.NotifyIconService>();

			containerRegistry.RegisterInstance(Dispatcher);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			ClosingEvent?.Publish();
		}
	}
}