using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Qzss.Services;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
	public int[] SerialBaudRates { get; } = [4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];

	// 更新レート(ms): 100ms=10Hz, 200ms=5Hz, 500ms=2Hz, 1000ms=1Hz
	public int[] SetupUpdateRates { get; } = [100, 200, 500, 1000];

	// ボーレート
	public int[] SetupBaudRates { get; } = [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];

	private bool _isSettingUp;
	public bool IsSettingUp
	{
		get => _isSettingUp;
		set
		{
			this.RaiseAndSetIfChanged(ref _isSettingUp, value);
			this.RaisePropertyChanged(nameof(CanRunSetup));
		}
	}

	public bool CanRunSetup => Connector.IsConnected && !IsSettingUp;

	private bool _hasSetupSteps;
	public bool HasSetupSteps
	{
		get => _hasSetupSteps;
		set => this.RaiseAndSetIfChanged(ref _hasSetupSteps, value);
	}

	public ObservableCollection<SetupStep> SetupSteps { get; } = [];

	public QzssSettingPage(KyoshinEewViewerConfiguration config, SerialConnector connector)
	{
		Config = config;
		Connector = connector;
		Connector.WhenAnyValue(x => x.IsConnected).Subscribe(_ => this.RaisePropertyChanged(nameof(CanRunSetup)));
	}

	public void UpdateSerialPorts() => SerialPorts = SerialPort.GetPortNames();

	// CFG-SIGNAL-QZSS_ENA
	private const uint CfgSignalQzssEna = 0x10310024u;
	// CFG-RATE-MEAS
	private const uint CfgRateMeas = 0x30210001u;
	// CFG-MSGOUT-UBX_RXM_SFRBX_UART1
	private const uint CfgMsgOutSfrbxUart1 = 0x20910232u;
	// CFG-MSGOUT-NMEA_ID_RMC_UART1
	private const uint CfgMsgOutRmcUart1 = 0x209100acu;
	// CFG-UART1-BAUDRATE
	private const uint CfgUart1BaudRate = 0x40520001u;

	public async Task SetupForUBlox()
	{
		var settingWindow = Locator.Current.GetService<ISubWindowsService>()?.SettingWindow;
		if (settingWindow == null)
			return;

		if (!Connector.IsConnected)
		{
			await new FAContentDialog
			{
				Title = "エラー",
				Content = "ポートに接続されていません。接続してから再度お試しください。",
				CloseButtonText = "OK"
			}.ShowAsync(settingWindow);
			return;
		}

		// 送信ステップを構築する
		var steps = new List<(SetupStep Step, Func<Task<bool>> Action)>();

		if (Config.Qzss.SetupSendSfrbx)
		{
			var step = new SetupStep("UART1 に UBX-RXM-SFRBX (衛星航法データ) 出力を有効化");
			steps.Add((step, () => Connector.SendCfgValSetAsync(CfgMsgOutSfrbxUart1, [0x01])));
		}
		if (Config.Qzss.SetupSendRmc)
		{
			var step = new SetupStep("UART1 に NMEA RMC 出力を有効化");
			steps.Add((step, () => Connector.SendCfgValSetAsync(CfgMsgOutRmcUart1, [0x01])));
		}
		if (Config.Qzss.SetupEnableQzss)
		{
			var step = new SetupStep("QZSS 受信を有効化");
			steps.Add((step, () => Connector.SendCfgValSetAsync(CfgSignalQzssEna, [0x01])));
		}
		if (Config.Qzss.SetupChangeUpdateRate)
		{
			var rate = (ushort)Config.Qzss.SetupUpdateRateMs;
			var step = new SetupStep($"更新レートを {rate}ms に変更");
			steps.Add((step, () => Connector.SendCfgValSetAsync(CfgRateMeas, [(byte)(rate & 0xFF), (byte)(rate >> 8)])));
		}

		// ボーレート変更は応答を受け取れなくなるため最後に実行(ACK 待機なし)
		SetupStep? baudRateStep = null;
		var changeBaudRate = Config.Qzss.SetupChangeBaudRate;
		if (changeBaudRate)
		{
			var baud = (uint)Config.Qzss.SetupBaudRate;
			baudRateStep = new SetupStep($"UART1 のボーレートを {baud} に変更");
		}

		if (steps.Count == 0 && baudRateStep == null)
		{
			await new FAContentDialog
			{
				Title = "設定項目なし",
				Content = "送信する設定項目が選択されていません。",
				CloseButtonText = "OK"
			}.ShowAsync(settingWindow);
			return;
		}

		SetupSteps.Clear();
		foreach (var (step, _) in steps)
			SetupSteps.Add(step);
		if (baudRateStep != null)
			SetupSteps.Add(baudRateStep);
		HasSetupSteps = true;

		IsSettingUp = true;
		var hasFailure = false;
		try
		{
			foreach (var (step, action) in steps)
			{
				step.Status = SetupStepStatus.Running;
				try
				{
					var ack = await action();
					step.Status = ack ? SetupStepStatus.Success : SetupStepStatus.Failed;
					if (!ack)
					{
						hasFailure = true;
						break;
					}
				}
				catch (Exception ex)
				{
					step.Message = ex.Message;
					step.Status = SetupStepStatus.Failed;
					hasFailure = true;
					break;
				}
				// 連続送信に対する余裕を持たせる
				await Task.Delay(50);
			}

			// 残りのステップを Skipped にマーク
			if (hasFailure)
			{
				foreach (var step in SetupSteps)
				{
					if (step.Status == SetupStepStatus.Pending)
						step.Status = SetupStepStatus.Skipped;
				}
			}
			else if (changeBaudRate && baudRateStep != null)
			{
				baudRateStep.Status = SetupStepStatus.Running;
				try
				{
					var baud = (uint)Config.Qzss.SetupBaudRate;
					await Connector.SendCfgValSetAsync(
						CfgUart1BaudRate,
						[(byte)(baud & 0xFF), (byte)((baud >> 8) & 0xFF), (byte)((baud >> 16) & 0xFF), (byte)((baud >> 24) & 0xFF)],
						waitAck: false);
					baudRateStep.Status = SetupStepStatus.Success;
					await Connector.ReconnectWithBaudRateAsync(Config.Qzss.SetupBaudRate);
				}
				catch (Exception ex)
				{
					baudRateStep.Message = ex.Message;
					baudRateStep.Status = SetupStepStatus.Failed;
					hasFailure = true;
				}
			}

			if (hasFailure)
			{
				await new FAContentDialog
				{
					Title = "設定送信失敗",
					Content = "一部の設定送信に失敗しました。詳細は進捗表示をご確認ください。",
					CloseButtonText = "OK"
				}.ShowAsync(settingWindow);
			}
			else
			{
				var msg = changeBaudRate
					? $"設定を送信しました。ボーレートを {Config.Qzss.SetupBaudRate} に変更して再接続しています。"
					: "設定を送信しました。";
				await new FAContentDialog
				{
					Title = "設定完了",
					Content = msg,
					CloseButtonText = "OK"
				}.ShowAsync(settingWindow);
			}
		}
		finally
		{
			IsSettingUp = false;
		}
	}
}

public enum SetupStepStatus
{
	Pending,
	Running,
	Success,
	Failed,
	Skipped,
}

public class SetupStep : ReactiveObject
{
	public string Name { get; }

	private SetupStepStatus _status = SetupStepStatus.Pending;
	public SetupStepStatus Status
	{
		get => _status;
		set
		{
			this.RaiseAndSetIfChanged(ref _status, value);
			this.RaisePropertyChanged(nameof(IsPending));
			this.RaisePropertyChanged(nameof(IsRunning));
			this.RaisePropertyChanged(nameof(IsSuccess));
			this.RaisePropertyChanged(nameof(IsFailed));
			this.RaisePropertyChanged(nameof(IsSkipped));
		}
	}

	public bool IsPending => Status == SetupStepStatus.Pending;
	public bool IsRunning => Status == SetupStepStatus.Running;
	public bool IsSuccess => Status == SetupStepStatus.Success;
	public bool IsFailed => Status == SetupStepStatus.Failed;
	public bool IsSkipped => Status == SetupStepStatus.Skipped;

	private string? _message;
	public string? Message
	{
		get => _message;
		set => this.RaiseAndSetIfChanged(ref _message, value);
	}

	public SetupStep(string name)
	{
		Name = name;
	}
}
