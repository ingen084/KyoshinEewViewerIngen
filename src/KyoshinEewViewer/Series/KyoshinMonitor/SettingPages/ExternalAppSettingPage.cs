using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using R3;

namespace KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;

/// <summary>
/// 外部アプリ連携 (SignalNow Professional / EEW Client) の設定ページ EEW Client の接続状態の表示も行う
/// </summary>
public partial class ExternalAppSettingPage : ObservableObject, ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => null;

	public string Title => "外部アプリ連携";

	public Control DisplayControl => new ExternalAppPage() { DataContext = this };

	public ISettingPage[] SubPages => [];

	public KyoshinEewViewerConfiguration Config { get; }
	public DmdataEewClientReceiver Receiver { get; }

	[ObservableProperty]
	public partial bool IsReceiving { get; private set; }

	[ObservableProperty]
	public partial bool IsConnectedNotReceiving { get; private set; }

	[ObservableProperty]
	public partial bool IsDisconnected { get; private set; }

	public ExternalAppSettingPage(KyoshinEewViewerConfiguration config, DmdataEewClientReceiver receiver)
	{
		Config = config;
		Receiver = receiver;

		Receiver.ObservePropertyChanged(x => x.State).Subscribe(state =>
		{
			IsReceiving = state == DmdataEewClientConnectionState.Receiving;
			IsConnectedNotReceiving = state == DmdataEewClientConnectionState.ConnectedNotReceiving;
			IsDisconnected = state == DmdataEewClientConnectionState.Disconnected;
		});
	}
}
