using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using R3;
using Splat;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;

public partial class ObservationPointsDataPage : UserControl
{
	public ObservationPointsDataPage()
	{
		InitializeComponent();
		DataContext = new ObservationPointsDataPageViewModel();
	}
}

public class ObservationPointsDataPageViewModel : ObservableObject
{
	private ObservationPointsUpdateService ObservationPointsUpdateService { get; }
	public KyoshinEewViewerConfiguration Config { get; }

	public ObservationPointsDataPageViewModel()
	{
		ObservationPointsUpdateService = Locator.Current.GetService<ObservationPointsUpdateService>()
			?? throw new InvalidOperationException("ObservationPointsUpdateServiceが登録されていません");
		Config = Locator.Current.GetService<KyoshinEewViewerConfiguration>()
			?? throw new InvalidOperationException("KyoshinEewViewerConfigurationが登録されていません");

		ManualUpdateCommand = new AsyncRelayCommand(ManualUpdateAsync);

		// 更新サービスからのステータス変更を監視
		this.ObservePropertyChanged(x => x.ObservationPointsUpdateService, x => x.IsUpdating)
			.Subscribe(x => OnPropertyChanged(nameof(IsUpdating)));
		this.ObservePropertyChanged(x => x.ObservationPointsUpdateService, x => x.UpdateStatus)
			.Subscribe(x => OnPropertyChanged(nameof(UpdateStatus)));

		// ヘッダ情報の変更を監視
		UpdateHeaderInfo();
	}

	public string DataVersion => ObservationPointsUpdateService.CurrentHeader?.DataVersion ?? "不明";
	public string PackedAt => ObservationPointsUpdateService.CurrentHeader?.PackedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "不明";
	public string Source => ObservationPointsUpdateService.CurrentHeader?.Source ?? "不明";
	public int ObservationPointsCount => ObservationPointsUpdateService.ObservationPointsCount;

	public bool IsUpdating => ObservationPointsUpdateService.IsUpdating;
	public string UpdateStatus => ObservationPointsUpdateService.UpdateStatus;

	public ICommand ManualUpdateCommand { get; }

	private async Task ManualUpdateAsync()
	{
		await ObservationPointsUpdateService.ManualUpdateAsync();
		UpdateHeaderInfo();
	}

	private void UpdateHeaderInfo()
	{
		OnPropertyChanged(nameof(DataVersion));
		OnPropertyChanged(nameof(PackedAt));
		OnPropertyChanged(nameof(Source));
		OnPropertyChanged(nameof(ObservationPointsCount));
	}
}
