using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels
{
	public class UpdateWindowViewModel : ReactiveObject
	{
		[Reactive]
		public string Title { get; set; } = "更新情報 - KyoshinEewViewer for ingen";

		public UpdateWindowViewModel()
		{
			if (Design.IsDesignMode)
			{
				VersionInfos = new VersionInfo[]
				{
					new VersionInfo
					{
						Time = DateTime.Now,
						Message = "test",
						VersionString = "1.1.31.0"
					},
				};
				return;
			}
			MessageBus.Current.Listen<UpdateFound>().Subscribe(a =>
			{
				VersionInfos = a.FoundUpdate;
			});
			VersionInfos = UpdateCheckService.Default.AvailableUpdateVersions;
		}

		private VersionInfo[]? versionInfos;

		public VersionInfo[]? VersionInfos
		{
			get => versionInfos;
			set => this.RaiseAndSetIfChanged(ref versionInfos, value);
		}

		public void OpenUrl(string url)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				url = url.Replace("&", "^&");
				Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				Process.Start("xdg-open", url);
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				Process.Start("open", url);
		}
	}
}
