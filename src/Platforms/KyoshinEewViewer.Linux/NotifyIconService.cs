using Avalonia;
using Avalonia.Platform;
using Gdk;
using Gtk;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application = Gtk.Application;

namespace KyoshinEewViewer.Linux
{
	public class NotifyIconService : INotifyIconService
	{
		private StatusIcon StatusIcon { get; }

		public NotifyIconService()
		{
			Application.Init();
			StatusIcon = new(new Pixbuf(AvaloniaLocator.Current.GetService<IAssetLoader>().Open(new Uri("avares://KyoshinEewViewer/Assets/logo.ico", UriKind.Absolute))));
			StatusIcon.TooltipText = "KyoshinEewViewer for ingen";
			StatusIcon.Activate += (s, e) => MessageBus.Current.SendMessage(new ShowMainWindowRequested());
			StatusIcon.PopupMenu += (s, e) => 
			{
				var popupMenu = new Menu();

				{
					var menuItem = new MenuItem("メインウィンドウを開く(&O)");
					menuItem.Activated += (s, e) => MessageBus.Current.SendMessage(new ShowMainWindowRequested());
					popupMenu.Add(menuItem);
				}
				{
					var menuItem = new MenuItem("設定(&S)");
					menuItem.Activated += (s, e) => MessageBus.Current.SendMessage(new ShowSettingWindowRequested());
					popupMenu.Add(menuItem);
				}
				popupMenu.Add(new SeparatorMenuItem());
				{
					var menuItem = new MenuItem("終了(&E)");
					menuItem.Activated += (s, e) => App.MainWindow?.Close();
					popupMenu.Add(menuItem);
				}

				popupMenu.ShowAll();
				popupMenu.Popup();
			};
			StatusIcon.Visible = false;
		}

		public bool Enabled { get => StatusIcon.Visible; set => StatusIcon.Visible = value; }

		public void Notify(string title, string message) { }
	}
}
