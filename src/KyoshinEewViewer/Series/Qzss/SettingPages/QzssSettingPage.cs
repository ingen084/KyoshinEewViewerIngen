using Avalonia.Controls;
using Avalonia.Threading;
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
using System.Linq;
using System.Threading;
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

	// 自動検出関連の状態
	private bool _isDetectingBaudRate;
	public bool IsDetectingBaudRate
	{
		get => _isDetectingBaudRate;
		private set
		{
			this.RaiseAndSetIfChanged(ref _isDetectingBaudRate, value);
			this.RaisePropertyChanged(nameof(CanDetectBaudRate));
			this.RaisePropertyChanged(nameof(CanEditConnection));
			this.RaisePropertyChanged(nameof(DetectBaudRateButtonLabel));
		}
	}

	private int? _currentDetectingBaudRate;
	public int? CurrentDetectingBaudRate
	{
		get => _currentDetectingBaudRate;
		private set
		{
			this.RaiseAndSetIfChanged(ref _currentDetectingBaudRate, value);
			this.RaisePropertyChanged(nameof(DetectBaudRateButtonLabel));
		}
	}

	public string DetectBaudRateButtonLabel => IsDetectingBaudRate
		? (CurrentDetectingBaudRate is { } rate ? $"キャンセル ({rate})" : "キャンセル")
		: "自動検出";

	// 未接続かつポート選択済み、かつ検出中でない場合のみボタンを有効化
	// (検出中はキャンセル動作のため別途有効化する。CanDetectBaudRate は「新規に押下可能か」のみを表す)
	public bool CanDetectBaudRate =>
		!Config.Qzss.Connect
		&& !IsDetectingBaudRate
		&& !string.IsNullOrWhiteSpace(Config.Qzss.SerialPort);

	// 検出中は接続関連 UI を編集不可にする
	public bool CanEditConnection => !Config.Qzss.Connect && !IsDetectingBaudRate;

	private CancellationTokenSource? _detectCts;

	public QzssSettingPage(KyoshinEewViewerConfiguration config, SerialConnector connector)
	{
		Config = config;
		Connector = connector;
		Connector.WhenAnyValue(x => x.IsConnected).Subscribe(_ => this.RaisePropertyChanged(nameof(CanRunSetup)));
		Config.Qzss.WhenAnyValue(x => x.Connect).Subscribe(_ =>
		{
			this.RaisePropertyChanged(nameof(CanDetectBaudRate));
			this.RaisePropertyChanged(nameof(CanEditConnection));
		});
		Config.Qzss.WhenAnyValue(x => x.SerialPort).Subscribe(_ =>
			this.RaisePropertyChanged(nameof(CanDetectBaudRate)));
	}

	public void UpdateSerialPorts() => SerialPorts = SerialPort.GetPortNames();

	/// <summary>
	/// ボーレート自動検出を実行する。検出中に再度呼ばれた場合はキャンセル要求として扱う。
	/// </summary>
	public async Task DetectOrCancelBaudRate()
	{
		// 検出中ならキャンセル
		if (IsDetectingBaudRate)
		{
			_detectCts?.Cancel();
			return;
		}

		if (!CanDetectBaudRate)
			return;

		var settingWindow = Locator.Current.GetService<ISubWindowsService>()?.SettingWindow;

		var portName = Config.Qzss.SerialPort;
		if (string.IsNullOrWhiteSpace(portName))
			return;

		// 試行順: 現在設定中のボーレート → SerialBaudRates の残り
		var currentBaudRate = Config.Qzss.BaudRate;
		var orderedBaudRates = new[] { currentBaudRate }
			.Concat(SerialBaudRates.Where(r => r != currentBaudRate))
			.ToArray();

		using var cts = new CancellationTokenSource();
		_detectCts = cts;
		IsDetectingBaudRate = true;
		CurrentDetectingBaudRate = null;

		int? detected = null;
		var canceled = false;
		Exception? error = null;
		try
		{
			detected = await Connector.DetectBaudRateAsync(
				portName,
				orderedBaudRates,
				perRateTimeoutMs: 1500,
				onProgress: rate => Dispatcher.UIThread.Post(() => CurrentDetectingBaudRate = rate),
				cancellationToken: cts.Token);
		}
		catch (OperationCanceledException)
		{
			canceled = true;
		}
		catch (Exception ex)
		{
			error = ex;
		}
		finally
		{
			_detectCts = null;
			IsDetectingBaudRate = false;
			CurrentDetectingBaudRate = null;
		}

		if (canceled)
			return;

		if (error != null)
		{
			if (settingWindow != null)
				await new ContentDialog
				{
					Title = "ボーレート自動検出 エラー",
					Content = $"自動検出中にエラーが発生しました。\n{error.Message}",
					CloseButtonText = "OK"
				}.ShowAsync(settingWindow);
			return;
		}

		if (detected is { } baudRate)
		{
			// 成功: ボーレートを設定し接続を開始する
			Config.Qzss.BaudRate = baudRate;
			Config.Qzss.Connect = true;
			return;
		}

		// 失敗: いずれのボーレートでも有効な受信を確認できなかった
		if (settingWindow != null)
			await new ContentDialog
			{
				Title = "ボーレートを自動検出できませんでした",
				Content = "いずれのボーレートでも有効なセンテンスを受信できませんでした。\n" +
					"ケーブル接続、デバイスの電源、デバイス側の設定を確認のうえ、手動でボーレートを選択してください。",
				CloseButtonText = "OK"
			}.ShowAsync(settingWindow);
	}

	// CFG-SIGNAL-QZSS_ENA
	private const uint CfgSignalQzssEna = 0x10310024u;
	// CFG-SIGNAL-QZSS_L1S_ENA
	private const uint CfgSignalQzssL1sEna = 0x10310014u;
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
			await new ContentDialog
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
			var enableStep = new SetupStep("QZSS 受信を有効化");
			steps.Add((enableStep, () => Connector.SendCfgValSetAsync(CfgSignalQzssEna, [0x01])));
			var l1sStep = new SetupStep("QZSS L1S (災危通報) 受信を有効化");
			steps.Add((l1sStep, () => Connector.SendCfgValSetAsync(CfgSignalQzssL1sEna, [0x01])));
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
			await new ContentDialog
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
				await new ContentDialog
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
				await new ContentDialog
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
