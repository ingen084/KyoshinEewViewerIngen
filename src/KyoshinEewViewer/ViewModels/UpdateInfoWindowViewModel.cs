using KyoshinEewViewer.Models;
using KyoshinEewViewer.Models.Events;
using KyoshinEewViewer.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Diagnostics;
using System.Windows.Input;

namespace KyoshinEewViewer.ViewModels
{
	public class UpdateInfoWindowViewModel : BindableBase, IDialogAware
	{
		private string _title = "更新情報";

		public string Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		public UpdateInfoWindowViewModel()
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
		}

		public UpdateInfoWindowViewModel(UpdateCheckService updateCheckService, IEventAggregator aggregator)
		{
			VersionInfos = updateCheckService.AliableUpdateVersions;
			aggregator.GetEvent<UpdateFound>().Subscribe(a =>
			{
				if (!a)
				{
					VersionInfos = null;
					return;
				}
				VersionInfos = updateCheckService.AliableUpdateVersions;
			});
		}

		private VersionInfo[] versionInfos;

		public VersionInfo[] VersionInfos
		{
			get => versionInfos;
			set => SetProperty(ref versionInfos, value);
		}

		private ICommand _openDownloadUrl;

		public ICommand OpenDownloadUrl => _openDownloadUrl ??= new DelegateCommand(() => Process.Start(new ProcessStartInfo("cmd", $"/c start https://ingen084.github.io/KyoshinEewViewer/") { CreateNoWindow = true }));


		public event Action<IDialogResult> RequestClose;

		public bool CanCloseDialog()
			=> true;

		public void OnDialogClosed()
		{
		}

		public void OnDialogOpened(IDialogParameters parameters)
		{
		}
	}
}