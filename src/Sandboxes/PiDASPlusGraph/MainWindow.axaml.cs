using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinMonitorLib;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace PiDASPlusGraph
{
	public partial class MainWindow : Window
	{
		private SerialPort Serial { get; } = new()
		{
			BaudRate = 115200,
			NewLine = "\r\n",
			ReadTimeout = 10000,
			DtrEnable = true,
			RtsEnable = true,
		};

		public MainWindow()
		{
			InitializeComponent();

			UpdateCover(true, "未接続");

			frameSkipSlider.WhenAnyValue(s => s.Value).Subscribe(v => FrameSkippableRenderTimer.SkipAmount = (uint)v);

			connectButton.Tapped += (s, e) =>
			{
				if (Serial.IsOpen)
				{
					Serial.Close();
					return;
				}
				if (selectDeviceBox.SelectedItem is not string port)
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
				connectButton.Content = "切断";
				Task.Run(SerialReceiveTask);
			};

			devicesUpdateButton.Tapped += (s, e) => UpdateSerialDevices();
			UpdateSerialDevices();

			intensityGraph.UpdateResources();
			intensityGraph.Data = new() { { SKColors.Red, IntensityHistory } };
			accGraph.UpdateResources();
			accGraph.Data = new() {
				{ SKColors.Tomato.WithAlpha(200), XHistory },
				{ SKColors.DarkCyan.WithAlpha(200), YHistory },
				{ SKColors.Orange.WithAlpha(200), ZHistory },
			};
		}

		private float[] IntensityHistory { get; } = new float[100];
		private float[] XHistory { get; } = new float[1000];
		private float[] YHistory { get; } = new float[1000];
		private float[] ZHistory { get; } = new float[1000];

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
						return;

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

					//Debug.WriteLine(line);

					switch (parts[0])
					{
						case "XSOFF" when parts.Length >= 2:
							UpdateCover(parts[1] == "1", "センサー調整中…");
							if (parts[1] == "0")
							{
								Array.Clear(IntensityHistory);
								Array.Clear(XHistory);
								Array.Clear(YHistory);
								Array.Clear(ZHistory);
							}
							break;
						case "XSINT" when parts.Length >= 3:
							Buffer.BlockCopy(IntensityHistory, 0, IntensityHistory, sizeof(float), (IntensityHistory.Length - 1) * sizeof(float));
							IntensityHistory[0] = Math.Clamp(float.TryParse(parts[2], out var ri) ? ri : IntensityHistory[1], intensityGraph.MinValue, intensityGraph.MaxValue);
							Dispatcher.UIThread.InvokeAsync(() =>
							{
								timeText.Text = DateTime.Now.ToString("MM/dd HH:mm:ss");
								rawIntText.Text = parts[2];
								intensity.Intensity = ((double)ri).ToJmaIntensity();
								intensityGraph.InvalidateVisual();
							});
							break;
						case "XSACC" when parts.Length >= 4:
							Buffer.BlockCopy(XHistory, 0, XHistory, sizeof(float), (XHistory.Length - 1) * sizeof(float));
							XHistory[0] = float.TryParse(parts[1], out var x) ? x : XHistory[1];
							Buffer.BlockCopy(YHistory, 0, YHistory, sizeof(float), (YHistory.Length - 1) * sizeof(float));
							YHistory[0] = float.TryParse(parts[2], out var y) ? y : YHistory[1];
							Buffer.BlockCopy(ZHistory, 0, ZHistory, sizeof(float), (ZHistory.Length - 1) * sizeof(float));
							ZHistory[0] = float.TryParse(parts[3], out var z) ? z : ZHistory[1];
							Dispatcher.UIThread.InvokeAsync(() =>
							{
								accXText.Text = parts[1] + "gal";
								accYText.Text = parts[2] + "gal";
								accZText.Text = parts[3] + "gal";
								accGraph.InvalidateVisual();
							});
							break;
					}
				}
			}
			catch (Exception e) when (e is TimeoutException || e is OperationCanceledException)
			{
				Serial.Close();
				Dispatcher.UIThread.InvokeAsync(() => connectButton.Content = "接続");
				UpdateCover(true, (e is TimeoutException) ? "受信できません" : "切断しました");
			}
		}

		private void UpdateSerialDevices()
		{
			var ports = SerialPort.GetPortNames().OrderBy(p => p).Distinct().ToArray();
			selectDeviceBox.Items = ports;
			if (selectDeviceBox.SelectedItem == null || !ports.Contains(selectDeviceBox.SelectedItem))
				selectDeviceBox.SelectedItem = ports.FirstOrDefault();
		}

		private void UpdateCover(bool visible, string? message = null)
		{
			if (!Dispatcher.UIThread.CheckAccess())
			{
				Dispatcher.UIThread.InvokeAsync(() => UpdateCover(visible, message));
				return;
			}
			if (adjustPanel.IsVisible = visible)
				adjustText.Text = message;
		}
	}
}
