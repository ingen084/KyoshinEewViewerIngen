using Avalonia.Controls;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Qzss.Services;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Qzss.SettingPages;

public class QzssSettingPage : ReactiveObject, ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => "\xf7bf";

	public string Title => "みちびき 災危通報";

	public Control DisplayControl => new QzssPage() { DataContext = this };

	public ISettingPage[] SubPages => [];

	public KyoshinEewViewerConfiguration Config { get; }
	public SerialConnector Connector { get; }

	private string[] _serialPorts = SerialPort.GetPortNames();
	public string[] SerialPorts
	{
		get => _serialPorts;
		set => this.RaiseAndSetIfChanged(ref _serialPorts, value);
	}
	public int[] SerialBaudRates { get; } = [4800, 9600, 19200, 38400, 57600, 115200];

	public QzssSettingPage(KyoshinEewViewerConfiguration config, SerialConnector connector)
	{
		Config = config;
		Connector = connector;
	}

	public void UpdateSerialPorts() => SerialPorts = SerialPort.GetPortNames();

	public async Task SetupForMaxM10S()
	{
		var settingWindow = Locator.Current.GetService<ISubWindowsService>()?.SettingWindow;
		if (settingWindow == null)
			return;

		try
		{
			await Connector.SetupForMaxM10SAsync();
			await new ContentDialog
			{
				Title = "設定完了",
				Content = "MAX-M10S設定を送信しました。\nボーレートを115200に変更して再接続しています。",
				CloseButtonText = "OK"
			}.ShowAsync(settingWindow);
		}
		catch (Exception ex)
		{
			await new ContentDialog
			{
				Title = "エラー",
				Content = ex.Message,
				CloseButtonText = "OK"
			}.ShowAsync(settingWindow);
		}
	}
}
