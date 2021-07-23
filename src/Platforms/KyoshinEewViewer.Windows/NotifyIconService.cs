using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Windows
{
	public class NotifyIconService : INotifyIconService
	{
		private System.Windows.Forms.NotifyIcon Icon { get; }
		public bool Enabled
		{
			get => Icon.Visible;
			set => Icon.Visible = value;
		}

		public NotifyIconService()
		{
			Icon = new System.Windows.Forms.NotifyIcon
			{
				Text = "KyoshinEewViewer for ingen",
				Icon = Properties.Resources.Icon,
				ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(),
			};
			Icon.ContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
			{
				new System.Windows.Forms.ToolStripMenuItem("メインウィンドウを開く(&O)", null, (s,e) => MessageBus.Current.SendMessage(new ShowMainWindowRequested())),
				new System.Windows.Forms.ToolStripMenuItem("設定(&S)", null, (s,e) => MessageBus.Current.SendMessage(new ShowSettingWindowRequested())),
				new System.Windows.Forms.ToolStripSeparator(),
				new System.Windows.Forms.ToolStripMenuItem("終了(&E)", null, (s,e) => App.MainWindow?.Close()),
			});
			Icon.DoubleClick += (s, e) => MessageBus.Current.SendMessage(new ShowMainWindowRequested());
			Icon.Visible = false;

			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => Icon.Dispose());
		}

		public void Notify(string title, string message) => Icon.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
	}
}
