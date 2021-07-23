using Avalonia;
using DynamicData.Binding;
using System;

namespace KyoshinEewViewer.Services
{
	public class NotificationService
	{
		private static NotificationService? _default;

		public static NotificationService Default => _default ??= new NotificationService();

		private INotifyIconService? NotifyIconService { get; }
		public bool Available => NotifyIconService?.Enabled ?? false;

		public NotificationService()
		{
			NotifyIconService = AvaloniaLocator.CurrentMutable.GetService<INotifyIconService>();
		}

		public void Initalize()
		{
			if (NotifyIconService is null)
				return;
			// TODO: ウィンドウ非表示中にOFFにされると戻ってこれなくなるので保留
			// ConfigurationService.Default.Notification.WhenValueChanged(x => x.Enable)
			//	 .Subscribe(x => NotifyIconService.Enabled = ConfigurationService.Default.Notification.Enable);
			NotifyIconService.Enabled = ConfigurationService.Default.Notification.Enable;
		}

		public void Notify(string title, string message)
		{
			if (ConfigurationService.Default.Notification.Enable)
				NotifyIconService?.Notify("[KEVi]" + title, message);
		}
	}

	public interface INotifyIconService
	{
		// event Action? Activated;
		bool Enabled { get; set; }
		void Notify(string title, string message);
	}
}
