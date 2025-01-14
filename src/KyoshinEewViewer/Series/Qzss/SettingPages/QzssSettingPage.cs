using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Qzss.Services;
using ReactiveUI;
using System.IO.Ports;

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
}
