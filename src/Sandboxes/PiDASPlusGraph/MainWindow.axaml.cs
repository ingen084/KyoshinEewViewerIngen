using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinMonitorLib;
using R3;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PiDASPlusGraph;

public partial class MainWindow : Window
{
	private const int AccSamplesPerSecond = 100;
	private const int IntSamplesPerSecond = 10;

	private SerialPort Serial { get; } = new()
	{
		BaudRate = 115200,
		NewLine = "\r\n",
		ReadTimeout = 10000,
		DtrEnable = true,
		RtsEnable = true,
	};

	private float[] IntensityHistory = new float[IntSamplesPerSecond * 10];
	private float[] XHistory = new float[AccSamplesPerSecond * 10];
	private float[] YHistory = new float[AccSamplesPerSecond * 10];
	private float[] ZHistory = new float[AccSamplesPerSecond * 10];
	private int WaveformSeconds = 10;
	private bool SplitAxes;
	private SettingWindow? _settingWindow;
	private TaskCompletionSource<(int, int, int)>? _displayConfigTcs;
	private Timer? _saveTimer;

	public MainWindow()
	{
		var config = App.Config;
		SplitAxes = config.SplitAxes;

		InitializeComponent();

		// ウィンドウ位置・サイズの復元
		if (!double.IsNaN(config.WindowWidth) && !double.IsNaN(config.WindowHeight))
			ClientSize = new Size(config.WindowWidth, config.WindowHeight);
		if (!double.IsNaN(config.WindowX) && !double.IsNaN(config.WindowY) &&
			config.WindowX != -32000 && config.WindowY != -32000)
		{
			WindowStartupLocation = WindowStartupLocation.Manual;
			Position = new PixelPoint((int)config.WindowX, (int)config.WindowY);
		}
		WindowState = (WindowState)config.WindowState;

		// グラフの初期設定を反映
		if (SplitAxes)
		{
			CombinedAccPanel.IsVisible = false;
			SplitAccPanel.IsVisible = true;
		}

		UpdateCover(true, "未接続");

		ConnectButton.Tapped += (s, e) =>
		{
			if (Serial.IsOpen)
			{
				Serial.Close();
				return;
			}
			if (SelectDeviceBox.SelectedItem is not string port)
				return;
			Serial.PortName = port;
			try
			{
				Serial.Open();
			}
			catch
			{
				UpdateCover(true, "オープンできません");
				return;
			}
			ConnectButtonIcon.Text = "\uf127";
			ToolTip.SetTip(ConnectButton, "切断");
			SelectDeviceBox.IsEnabled = false;
			DevicesUpdateButton.IsEnabled = false;
			ClearObservationData();
			try
			{
				Serial.WriteLine("HWINFO");
			}
			catch
			{
				// HWINFO送信失敗は致命的ではないので続行
			}
			_settingWindow?.UpdateDisplayConfigState(true);
			Task.Run(SerialReceiveTask);
		};

		DevicesUpdateButton.Tapped += (s, e) => UpdateSerialDevices();
		SettingButton.Tapped += (s, e) => OpenSettingWindow();
		UpdateSerialDevices();

		// 保存済みのポートを選択
		if (config.SelectedPort != null && SelectDeviceBox.ItemsSource is string[] ports &&
			ports.Contains(config.SelectedPort))
			SelectDeviceBox.SelectedItem = config.SelectedPort;

		// 波形の秒数に合わせてバッファを調整
		ApplyWaveformSeconds(config.WaveformSeconds);

		UpdateGraphResources();
		IntensityGraph.Data = new Dictionary<SKColor, float[]> { { SKColors.Red, IntensityHistory } };
		UpdateAccGraphData();

		if (App.Selector is { } selector)
		{
			selector.ObservePropertyChanged(x => x.SelectedWindowTheme)
				.Where(x => x != null)
				.Subscribe(_ => Dispatcher.UIThread.Post(() =>
				{
					UpdateGraphResources();
					IntensityGraph.InvalidateVisual();
					InvalidateAccGraphs();
					Intensity.InvalidateVisual();
				}));
			selector.ObservePropertyChanged(x => x.SelectedIntensityTheme)
				.Where(x => x != null)
				.Subscribe(_ => Dispatcher.UIThread.Post(() =>
				{
					Intensity.InvalidateVisual();
				}));
		}

		Closing += (_, _) => SaveConfig();
		_saveTimer = new Timer(_ => Dispatcher.UIThread.Post(SaveConfig), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
	}

	private void SaveConfig()
	{
		var config = App.Config;
		config.WaveformSeconds = WaveformSeconds;
		config.SplitAxes = SplitAxes;
		config.SelectedPort = SelectDeviceBox.SelectedItem as string;
		config.WindowState = (int)WindowState;
		if (WindowState is not Avalonia.Controls.WindowState.Minimized and not Avalonia.Controls.WindowState.FullScreen)
		{
			config.WindowX = Position.X;
			config.WindowY = Position.Y;
			if (WindowState != Avalonia.Controls.WindowState.Maximized)
			{
				config.WindowWidth = ClientSize.Width;
				config.WindowHeight = ClientSize.Height;
			}
		}
		App.SaveConfig();
	}

	private void OpenSettingWindow()
	{
		if (_settingWindow != null)
		{
			_settingWindow.Activate();
			return;
		}
		_settingWindow = new SettingWindow(
			WaveformSeconds,
			SplitAxes,
			ApplyWaveformSeconds,
			ApplySplitAxes,
			QueryDisplayConfigAsync,
			SendSerialCommand);
		_settingWindow.Closed += (_, _) => _settingWindow = null;
		_settingWindow.Show(this);
		App.ApplyDwmAttributes(_settingWindow);
	}

	private async Task<(int current, int max, int reset)?> QueryDisplayConfigAsync()
	{
		if (!Serial.IsOpen)
			return null;

		var tcs = new TaskCompletionSource<(int, int, int)>();
		_displayConfigTcs = tcs;
		try
		{
			Serial.WriteLine("DSPCFG");
		}
		catch
		{
			_displayConfigTcs = null;
			return null;
		}

		try
		{
			if (await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task)
				return tcs.Task.Result;

			tcs.TrySetCanceled();
			return null;
		}
		finally
		{
			_displayConfigTcs = null;
		}
	}

	private void SendSerialCommand(string command)
	{
		if (!Serial.IsOpen)
			return;
		try
		{
			Serial.WriteLine(command);
		}
		catch
		{
		}
	}

	private void ApplyWaveformSeconds(int seconds)
	{
		if (seconds == WaveformSeconds)
			return;
		WaveformSeconds = seconds;

		var accSamples = seconds * AccSamplesPerSecond;
		var intSamples = seconds * IntSamplesPerSecond;

		var newInt = new float[intSamples];
		Array.Fill(newInt, float.NaN);
		var newX = new float[accSamples];
		var newY = new float[accSamples];
		var newZ = new float[accSamples];

		Array.Copy(IntensityHistory, newInt, Math.Min(intSamples, IntensityHistory.Length));
		Array.Copy(XHistory, newX, Math.Min(accSamples, XHistory.Length));
		Array.Copy(YHistory, newY, Math.Min(accSamples, YHistory.Length));
		Array.Copy(ZHistory, newZ, Math.Min(accSamples, ZHistory.Length));

		IntensityHistory = newInt;
		XHistory = newX;
		YHistory = newY;
		ZHistory = newZ;

		IntensityGraph.Data = new Dictionary<SKColor, float[]> { { SKColors.Red, IntensityHistory } };
		UpdateAccGraphData();

		IntensityGraph.InvalidateVisual();
		InvalidateAccGraphs();
	}

	private void ApplySplitAxes(bool split)
	{
		SplitAxes = split;
		CombinedAccPanel.IsVisible = !split;
		SplitAccPanel.IsVisible = split;
		InvalidateAccGraphs();
	}

	private void UpdateGraphResources()
	{
		IntensityGraph.UpdateResources();
		AccGraph.UpdateResources();
		AccXGraph.UpdateResources();
		AccYGraph.UpdateResources();
		AccZGraph.UpdateResources();
	}

	private void UpdateAccGraphData()
	{
		AccGraph.Data = new Dictionary<SKColor, float[]> {
			{ SKColors.Tomato.WithAlpha(200), XHistory },
			{ SKColors.DarkCyan.WithAlpha(200), YHistory },
			{ SKColors.Orange.WithAlpha(200), ZHistory },
		};
		AccXGraph.Data = new Dictionary<SKColor, float[]> { { SKColors.Tomato.WithAlpha(200), XHistory } };
		AccYGraph.Data = new Dictionary<SKColor, float[]> { { SKColors.DarkCyan.WithAlpha(200), YHistory } };
		AccZGraph.Data = new Dictionary<SKColor, float[]> { { SKColors.Orange.WithAlpha(200), ZHistory } };
	}

	private void InvalidateAccGraphs()
	{
		if (SplitAxes)
		{
			AccXGraph.InvalidateVisual();
			AccYGraph.InvalidateVisual();
			AccZGraph.InvalidateVisual();
		}
		else
		{
			AccGraph.InvalidateVisual();
		}
	}

	private void SerialReceiveTask()
	{
		try
		{
			UpdateCover(false);
			while (Serial.IsOpen)
			{
				var line = Serial.ReadLine();
				// NMEAセンテンスであることを確認
				if (line.Length < 1 || line[0] != '$')
					continue;

				// チェックサム確認
				var csIndex = line.IndexOf('*');
				string[] parts;
				if (csIndex != -1)
				{
					parts = line[1..csIndex].Split(',');
					// チェックサムを取得
					var chS = line[(csIndex + 1)..];
					byte checkSum = 0;
					foreach (var b in line[1..csIndex])
						checkSum ^= (byte)b;
					if (chS != checkSum.ToString("X2"))
					{
						Debug.WriteLine("cserr: " + line[1..csIndex]);
						continue;
					}
				}
				// チェックサムなし
				else
					parts = line[1..].Split(',');

				switch (parts[0])
				{
					case "XSOFF" when parts.Length >= 2:
						UpdateCover(parts[1] == "1", "センサー調整中…");
						if (parts[1] == "0")
							ClearObservationData();
						break;
					case "XSINT" when parts.Length >= 3:
						var intHistory = IntensityHistory;
						Buffer.BlockCopy(intHistory, 0, intHistory, sizeof(float), (intHistory.Length - 1) * sizeof(float));
						intHistory[0] = Math.Clamp(float.TryParse(parts[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ri) ? ri : intHistory[1], IntensityGraph.MinValue, IntensityGraph.MaxValue);
						Dispatcher.UIThread.Post(() =>
						{
							TimeText.Text = DateTime.Now.ToString("MM/dd HH:mm:ss");
							RawIntText.Text = parts[2];
							Intensity.Intensity = float.IsNaN(ri) ? JmaIntensity.Unknown : ((double)ri).ToJmaIntensity();
							IntensityGraph.InvalidateVisual();
						});
						break;
					case "XSACC" when parts.Length >= 4:
						var xh = XHistory;
						var yh = YHistory;
						var zh = ZHistory;
						Buffer.BlockCopy(xh, 0, xh, sizeof(float), (xh.Length - 1) * sizeof(float));
						xh[0] = float.TryParse(parts[1], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x) ? x : xh[1];
						Buffer.BlockCopy(yh, 0, yh, sizeof(float), (yh.Length - 1) * sizeof(float));
						yh[0] = float.TryParse(parts[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y) ? y : yh[1];
						Buffer.BlockCopy(zh, 0, zh, sizeof(float), (zh.Length - 1) * sizeof(float));
						zh[0] = float.TryParse(parts[3], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var z) ? z : zh[1];
						Dispatcher.UIThread.Post(() =>
						{
							AccXText.Text = parts[1] + "gal";
							AccYText.Text = parts[2] + "gal";
							AccZText.Text = parts[3] + "gal";
							InvalidateAccGraphs();
						});
						break;
					case "XSPGA" when parts.Length >= 2:
						Dispatcher.UIThread.Post(() =>
						{
							PgaText.Text = parts[1] + "gal";
						});
						break;
					case "XSCFG" when parts.Length >= 5 && parts[1] == "DSP":
						if (int.TryParse(parts[2], out var dspCurrent) &&
							int.TryParse(parts[3], out var dspMax) &&
							int.TryParse(parts[4], out var dspReset))
							_displayConfigTcs?.TrySetResult((dspCurrent, dspMax, dspReset));
						break;
					case "XSHWI" when parts.Length >= 7:
						Dispatcher.UIThread.Post(() =>
						{
							HwInfoText.Text = $"{parts[3]} / FW:{parts[2]}";
							ToolTip.SetTip(HwInfoText,
								$"情報バージョン: {parts[1]}\n" +
								$"ファームウェア: {parts[2]}\n" +
								$"ハードウェア: {parts[3]}\n" +
								$"センサー: {parts[4]}\n" +
								$"ADC: {parts[5]}\n" +
								$"生データステップ: {parts[6]}gal");
						});
						break;
				}
			}
		}
		catch (Exception e) when (e is TimeoutException || e is OperationCanceledException || e is IOException)
		{
		}
		finally
		{
			Serial.Close();
			Dispatcher.UIThread.Post(() =>
			{
				ConnectButtonIcon.Text = "\uf0c1";
				ToolTip.SetTip(ConnectButton, "接続");
				SelectDeviceBox.IsEnabled = true;
				DevicesUpdateButton.IsEnabled = true;
				HwInfoText.Text = "ボード情報: 未取得";
				ToolTip.SetTip(HwInfoText, null);
				_settingWindow?.UpdateDisplayConfigState(false);
			});
			UpdateCover(true, "切断しました");
		}
	}

	private void UpdateSerialDevices()
	{
		var ports = SerialPort.GetPortNames().OrderBy(p => p).Distinct().ToArray();
		SelectDeviceBox.ItemsSource = ports;
		if (SelectDeviceBox.SelectedItem == null || !ports.Contains(SelectDeviceBox.SelectedItem))
			SelectDeviceBox.SelectedItem = ports.FirstOrDefault();
	}

	private void ClearObservationData()
	{
		Array.Fill(IntensityHistory, float.NaN);
		Array.Clear(XHistory);
		Array.Clear(YHistory);
		Array.Clear(ZHistory);
	}

	private void UpdateCover(bool visible, string? message = null)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => UpdateCover(visible, message));
			return;
		}
		if (AdjustPanel.IsVisible = visible)
			AdjustText.Text = message;
	}
}
