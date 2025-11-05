using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using ReactiveUI;
using Splat;
using System;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;

public partial class ObservationPointsDataPage : UserControl
{
	public ObservationPointsDataPage()
	{
		InitializeComponent();
		DataContext = new ObservationPointsDataPageViewModel();
	}
}

public class ObservationPointsDataPageViewModel : ReactiveObject
{
	private ObservationPointsUpdateService ObservationPointsUpdateService { get; }
	public KyoshinEewViewerConfiguration Config { get; }

	public ObservationPointsDataPageViewModel()
	{
		ObservationPointsUpdateService = Locator.Current.GetService<ObservationPointsUpdateService>()
			?? throw new InvalidOperationException("ObservationPointsUpdateServiceが登録されていません");
		Config = Locator.Current.GetService<KyoshinEewViewerConfiguration>()
			?? throw new InvalidOperationException("KyoshinEewViewerConfigurationが登録されていません");

		ManualUpdateCommand = ReactiveCommand.CreateFromTask(ManualUpdateAsync);

		// 更新サービスからのステータス変更を監視
		this.WhenAnyValue(x => x.ObservationPointsUpdateService.IsUpdating)
			.Subscribe(x => this.RaisePropertyChanged(nameof(IsUpdating)));
		this.WhenAnyValue(x => x.ObservationPointsUpdateService.UpdateStatus)
			.Subscribe(x => this.RaisePropertyChanged(nameof(UpdateStatus)));

		// ヘッダ情報の変更を監視
		UpdateHeaderInfo();
	}

	public string DataVersion => ObservationPointsUpdateService.GetCurrentHeader()?.DataVersion ?? "不明";
	public string PackedAt => ObservationPointsUpdateService.GetCurrentHeader()?.PackedAt.ToString("yyyy/MM/dd HH:mm:ss") ?? "不明";
	public string Source => ObservationPointsUpdateService.GetCurrentHeader()?.Source ?? "不明";
	public int ObservationPointsCount => ObservationPointsUpdateService.GetObservationPointsCount();

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
		this.RaisePropertyChanged(nameof(DataVersion));
		this.RaisePropertyChanged(nameof(PackedAt));
		this.RaisePropertyChanged(nameof(Source));
		this.RaisePropertyChanged(nameof(ObservationPointsCount));
	}
}
