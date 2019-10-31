using Prism.Events;
using System.Windows.Forms;

namespace KyoshinEewViewer.Services
{
	public class NotifyIconService
	{
		private NotifyIcon Icon { get; }

		public NotifyIconService(ConfigurationService configService, IEventAggregator aggregator)
		{
			Icon = new NotifyIcon
			{
				Text = "KyoshinEewViewer for ingen",
				Icon = Properties.Resources.Icon,
				ContextMenuStrip = new ContextMenuStrip(),
			};
			Icon.ContextMenuStrip.Items.AddRange(new ToolStripItem[]
			{
				new ToolStripMenuItem("メインウィンドウを開く(&O)", null, (s,e) => aggregator.GetEvent<Events.ShowMainWindowRequested>().Publish()),
				new ToolStripMenuItem("設定(&S)", null, (s,e) => aggregator.GetEvent<Events.ShowSettingWindowRequested>().Publish()),
				new ToolStripSeparator(),
				new ToolStripMenuItem("終了(&E)", null, (s,e) => System.Windows.Application.Current.Shutdown()),
			});
			Icon.DoubleClick += (s, e) => aggregator.GetEvent<Events.ShowMainWindowRequested>().Publish();
			Icon.Visible = configService.Configuration.EnableNotifyIcon;

			aggregator.GetEvent<Events.ApplicationClosing>().Subscribe(() => Icon.Dispose());
		}
	}
}