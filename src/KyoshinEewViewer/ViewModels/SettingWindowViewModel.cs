using KyoshinEewViewer.Models;
using KyoshinEewViewer.Models.Events;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace KyoshinEewViewer.ViewModels
{
	public class SettingWindowViewModel : BindableBase, IDialogAware
	{
		private ThemeService ThemeService { get; }
		private ConfigurationService ConfigService { get; }
		private DmdataService DmdataService { get; }
		public KyoshinEewViewerConfiguration Config { get; }

#if DEBUG
		public SettingWindowViewModel()
		{
			Config = new KyoshinEewViewerConfiguration();
			Config.Timer.Offset = 2500;
			Config.Theme.WindowThemeName = "Light";
			Config.Theme.IntensityThemeName = "Standard";

			AvailableDmdataBillingInfo = true;
			DmdataTotalBillingAmount = 5000;
			DmdataCreditAmount = 20000;
			DmdataBillingStatusUpdatedTime = DateTime.Now;
			DmdataBillingStatusTargetMonth = DateTime.Now;

			Ints = new List<JmaIntensity> {
				JmaIntensity.Unknown,
				JmaIntensity.Int0,
				JmaIntensity.Int1,
				JmaIntensity.Int2,
				JmaIntensity.Int3,
				JmaIntensity.Int4,
				JmaIntensity.Int5Lower,
				JmaIntensity.Int5Upper,
				JmaIntensity.Int6Lower,
				JmaIntensity.Int6Upper,
				JmaIntensity.Int7,
				JmaIntensity.Error,
			};
		}
#endif
		private string title = "KyoshinEewViewer 設定";
		public string Title
		{
			get => title;
			set => SetProperty(ref title, value);
		}

		private ICommand applyDmdataApiKeyCommand;
		public ICommand ApplyDmdataApiKeyCommand => applyDmdataApiKeyCommand ??= new DelegateCommand(() => Config.Dmdata.ApiKey = DmdataApiKey);

		public List<JmaIntensity> Ints { get; }

		private ICommand _registMapPositionCommand;
		public ICommand RegistMapPositionCommand => _registMapPositionCommand ??= new DelegateCommand(() => Aggregator.GetEvent<RegistMapPositionRequested>().Publish());

		private ICommand _resetMapPositionCommand;
		public ICommand ResetMapPositionCommand => _resetMapPositionCommand ??= new DelegateCommand(() =>
		{
			Config.Map.Location1 = new Location(24.058240f, 123.046875f);
			Config.Map.Location2 = new Location(45.706479f, 146.293945f);
		});

		private string timeshiftSecondsString = "リアルタイム";
		public string TimeshiftSecondsString
		{
			get => timeshiftSecondsString;
			set => SetProperty(ref timeshiftSecondsString, value);
		}

		private string dmdataStatus = "未実装です";
		public string DmdataStatusString
		{
			get => dmdataStatus;
			set => SetProperty(ref dmdataStatus, value);
		}
		private bool availableBillingInfo = false;
		public bool AvailableDmdataBillingInfo
		{
			get => availableBillingInfo;
			set => SetProperty(ref availableBillingInfo, value);
		}
		private int dmdataTotalBillingAmount = 0;
		public int DmdataTotalBillingAmount
		{
			get => dmdataTotalBillingAmount;
			set => SetProperty(ref dmdataTotalBillingAmount, value);
		}
		private int dmdataCreditAmount = 0;
		public int DmdataCreditAmount
		{
			get => dmdataCreditAmount;
			set => SetProperty(ref dmdataCreditAmount, value);
		}

		private string dmdataApiKey;
		public string DmdataApiKey
		{
			get => dmdataApiKey;
			set => SetProperty(ref dmdataApiKey, value);
		}
		private DateTime dmdataBillingStatusUpdatedTime;
		public DateTime DmdataBillingStatusUpdatedTime
		{
			get => dmdataBillingStatusUpdatedTime;
			set => SetProperty(ref dmdataBillingStatusUpdatedTime, value);
		}
		private DateTime dmdataBillingTargetMonth;
		public DateTime DmdataBillingStatusTargetMonth
		{
			get => dmdataBillingTargetMonth;
			set => SetProperty(ref dmdataBillingTargetMonth, value);
		}

		private IEventAggregator Aggregator { get; }
		public SettingWindowViewModel(ThemeService service, DmdataService dmdataService, ConfigurationService configService, IEventAggregator aggregator)
		{
			ThemeService = service;
			Aggregator = aggregator;
			ConfigService = configService;
			Config = ConfigService.Configuration;
			DmdataService = dmdataService;

			SelectedWindowTheme = ThemeService.WindowThemes.FirstOrDefault(t => t.Value == Config.Theme.WindowThemeName);
			SelectedIntensityTheme = ThemeService.IntensityThemes.FirstOrDefault(t => t.Value == Config.Theme.IntensityThemeName);

			Ints = new List<JmaIntensity> {
				JmaIntensity.Unknown,
				JmaIntensity.Int0,
				JmaIntensity.Int1,
				JmaIntensity.Int2,
				JmaIntensity.Int3,
				JmaIntensity.Int4,
				JmaIntensity.Int5Lower,
				JmaIntensity.Int5Upper,
				JmaIntensity.Int6Lower,
				JmaIntensity.Int6Upper,
				JmaIntensity.Int7,
			};

			Config.Timer.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName != nameof(Config.Timer.TimeshiftSeconds))
					return;
				UpdateTimeshiftString();
			};
			UpdateTimeshiftString();

			DmdataApiKey = Config.Dmdata.ApiKey;
			Aggregator.GetEvent<DmdataBillingInfoUpdated>().Subscribe(UpdateDmdataBillingInfo);
			UpdateDmdataBillingInfo();
			Aggregator.GetEvent<DmdataStatusUpdated>().Subscribe(UpdateDmdataStatus);
			UpdateDmdataStatus();
		}
		private void UpdateDmdataStatus()
		{
			DmdataStatusString = DmdataService.Status switch
			{
				DmdataStatus.Stopping => "APIキーが入力されていません",
				DmdataStatus.StoppingForInvalidKey => "APIキーが間違っているか、権限がありません",
				DmdataStatus.Failed => "取得中に問題が発生しました",
				DmdataStatus.UsingPullForForbidden => "PULL型利用中(WebSocket権限不足)",
				DmdataStatus.UsingPullForError => "PULL型利用中(同時接続数不足 or 管理画面からの切断)",
				DmdataStatus.UsingPull => "PULL型利用中",
				DmdataStatus.ReconnectingWebSocket => "WebSocket再接続中",
				DmdataStatus.UsingWebSocket => "WebSocket接続中",
				DmdataStatus.Initalizing => "過去データ受信中",
				_ => "不明",
			};
		}
		private void UpdateDmdataBillingInfo()
		{
			if (DmdataService.BillingInfo is null)
			{
				AvailableDmdataBillingInfo = false;
				return;
			}
			AvailableDmdataBillingInfo = true;
			DmdataTotalBillingAmount = DmdataService.BillingInfo.Amount?.Total ?? -1;
			DmdataCreditAmount = DmdataService.BillingInfo.Unpaid;
			DmdataBillingStatusUpdatedTime = DateTime.Now;
			DmdataBillingStatusTargetMonth = DmdataService.BillingInfo.Date;
		}
		private void UpdateTimeshiftString()
		{
			if (Config.Timer.TimeshiftSeconds == 0)
			{
				TimeshiftSecondsString = "リアルタイム";
				return;
			}

			var sb = new StringBuilder();
			var time = TimeSpan.FromSeconds(-Config.Timer.TimeshiftSeconds);
			if (time.TotalHours >= 1)
				sb.Append((int)time.TotalHours + "時間");
			if (time.Minutes > 0)
				sb.Append(time.Minutes + "分");
			if (time.Seconds > 0)
				sb.Append(time.Seconds + "秒");
			sb.Append('前');

			TimeshiftSecondsString = sb.ToString();
		}

		public static IReadOnlyDictionary<string, string> WindowThemes => ThemeService.WindowThemes;
		public static IReadOnlyDictionary<string, string> IntensityThemes => ThemeService.IntensityThemes;

		private KeyValuePair<string, string> selectedWindowTheme;

		public KeyValuePair<string, string> SelectedWindowTheme
		{
			get => selectedWindowTheme;
			set
			{
				if (SelectedWindowTheme.Value != value.Value)
					ThemeService.WindowThemeId = value.Value;
				SetProperty(ref selectedWindowTheme, value);
			}
		}

		private KeyValuePair<string, string> selectedIntensityTheme;

		public KeyValuePair<string, string> SelectedIntensityTheme
		{
			get => selectedIntensityTheme;
			set
			{
				if (SelectedIntensityTheme.Value != value.Value)
					ThemeService.IntensityThemeId = value.Value;
				SetProperty(ref selectedIntensityTheme, value);
			}
		}

		private ICommand _openUrl;
		public ICommand OpenUrl => _openUrl ??= new DelegateCommand<string>(u => Process.Start(new ProcessStartInfo("cmd", $"/c start {u.Replace("&", "^&")}") { CreateNoWindow = true }));


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