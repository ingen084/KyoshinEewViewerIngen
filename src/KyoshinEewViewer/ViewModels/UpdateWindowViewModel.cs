using Avalonia.Controls;
using KyoshinEewViewer.Models;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;

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
			UpdateCheckService.Default.Updated += a =>
			{
				VersionInfos = a;
			};
			VersionInfos = UpdateCheckService.Default.AvailableUpdateVersions;
		}

		private VersionInfo[]? versionInfos;

		public VersionInfo[]? VersionInfos
		{
			get => versionInfos;
			set => this.RaiseAndSetIfChanged(ref versionInfos, value);
		}

		public static void OpenUrl(string url)
			=> UrlOpener.OpenUrl(url);
	}
}
