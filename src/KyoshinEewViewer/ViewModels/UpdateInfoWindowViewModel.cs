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

#if DEBUG
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
#endif

		public UpdateInfoWindowViewModel(UpdateCheckService updateCheckService, IEventAggregator aggregator)
		{
			aggregator.GetEvent<UpdateFound>().Subscribe(a =>
			{
				if (!a)
				{
					VersionInfos = null;
					return;
				}
				VersionInfos = updateCheckService.AliableUpdateVersions;
			});
			VersionInfos = updateCheckService.AliableUpdateVersions;
		}

		private VersionInfo[] versionInfos;

		public VersionInfo[] VersionInfos
		{
			get => versionInfos;
			set => SetProperty(ref versionInfos, value);
		}

		private ICommand _openDownloadUrl;

		public ICommand OpenDownloadUrl => _openDownloadUrl ??= new DelegateCommand(() => Process.Start(new ProcessStartInfo("cmd", $"/c start https://ingen084.github.io/KyoshinEewViewer/") { CreateNoWindow = true }));


#pragma warning disable CS0067
		public event Action<IDialogResult> RequestClose;
#pragma warning restore CS0067

		public bool CanCloseDialog()
			=> true;

		public bool IsDialogOpening { get; set; }
		public void OnDialogClosed()
		{
			IsDialogOpening = false;
		}

		public void OnDialogOpened(IDialogParameters parameters)
		{
			IsDialogOpening = true;
		}
	}
}