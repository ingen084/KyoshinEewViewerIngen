using Prism.Events;
using System;
using System.Windows.Forms;

namespace KyoshinEewViewer.Services
{
	public class NotifyIconService
	{
		private NotifyIcon Icon { get; }

		public NotifyIconService(IEventAggregator aggregator)
		{
			Icon = new NotifyIcon
			{
				Text = "KyoshinEewViewer for ingen",
				Icon = Properties.Resources.Icon,
				ContextMenuStrip = new ContextMenuStrip(),
			};
			Icon.ContextMenuStrip.Items.AddRange(new ToolStripItem[]
			{
				new ToolStripMenuItem("設定(&S)", null, (s,e) => aggregator.GetEvent<Events.ShowSettingWindowRequested>().Publish()),
				new ToolStripSeparator(),
				new ToolStripMenuItem("終了(&E)", null, (s,e) => System.Windows.Application.Current.Shutdown()),
			});
			Icon.Visible = true;

			aggregator.GetEvent<Events.ApplicationClosing>().Subscribe(() => Icon.Dispose());
		}
	}
}