using Prism.Ioc;
using KyoshinEewViewer.Views;
using System.Windows;
using Prism.Events;

namespace KyoshinEewViewer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		private Events.ApplicationClosing ClosingEvent { get; set; }

		protected override Window CreateShell()
		{
			var aggregator = Container.Resolve<IEventAggregator>();
			ClosingEvent = aggregator.GetEvent<Events.ApplicationClosing>();
			return Container.Resolve<MainWindow>();
		}

		protected override void RegisterTypes(IContainerRegistry containerRegistry)
		{
			containerRegistry.RegisterSingleton<Services.LoggerService>();
			containerRegistry.RegisterSingleton<Services.ConfigurationService>();
			containerRegistry.RegisterSingleton<Services.ThemeService>();
			containerRegistry.RegisterSingleton<Services.TrTimeTableService>();
			containerRegistry.RegisterSingleton<Services.KyoshinMonitorWatchService>();
			containerRegistry.RegisterSingleton<Services.UpdateCheckService>();
			containerRegistry.RegisterSingleton<Services.TimerService>();

			containerRegistry.RegisterInstance(Dispatcher);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			ClosingEvent?.Publish();
		}
	}
}
