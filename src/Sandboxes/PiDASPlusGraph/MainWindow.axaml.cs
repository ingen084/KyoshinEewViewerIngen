using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinMonitorLib;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
				ConnectButton.Content = "切断";
				Task.Run(SerialReceiveTask);
			};

			DevicesUpdateButton.Tapped += (s, e) => UpdateSerialDevices();
			UpdateSerialDevices();

			IntensityGraph.UpdateResources();
			IntensityGraph.Data = new Dictionary<SKColor, float[]> { { SKColors.Red, IntensityHistory } };
			AccGraph.UpdateResources();
			AccGraph.Data = new Dictionary<SKColor, float[]> {
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
							IntensityHistory[0] = Math.Clamp(float.TryParse(parts[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ri) ? ri : IntensityHistory[1], IntensityGraph.MinValue, IntensityGraph.MaxValue);
							Dispatcher.UIThread.Post(() =>
							{
								TimeText.Text = DateTime.Now.ToString("MM/dd HH:mm:ss");
								RawIntText.Text = parts[2];
								Intensity.Intensity = float.IsNaN(ri) ? JmaIntensity.Unknown : ((double)ri).ToJmaIntensity();
								IntensityGraph.InvalidateVisual();
							});
							break;
						case "XSACC" when parts.Length >= 4:
							Buffer.BlockCopy(XHistory, 0, XHistory, sizeof(float), (XHistory.Length - 1) * sizeof(float));
							XHistory[0] = float.TryParse(parts[1], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x) ? x : XHistory[1];
							Buffer.BlockCopy(YHistory, 0, YHistory, sizeof(float), (YHistory.Length - 1) * sizeof(float));
							YHistory[0] = float.TryParse(parts[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y) ? y : YHistory[1];
							Buffer.BlockCopy(ZHistory, 0, ZHistory, sizeof(float), (ZHistory.Length - 1) * sizeof(float));
							ZHistory[0] = float.TryParse(parts[3], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var z) ? z : ZHistory[1];
							Dispatcher.UIThread.Post(() =>
							{
								AccXText.Text = parts[1] + "gal";
								AccYText.Text = parts[2] + "gal";
								AccZText.Text = parts[3] + "gal";
								AccGraph.InvalidateVisual();
							});
							break;
						case "XSPGA" when parts.Length >= 2:
							Dispatcher.UIThread.Post(() =>
							{
								PgaText.Text = parts[1] + "gal";
							});
							break;
					}
				}
			}
			catch (Exception e) when (e is TimeoutException || e is OperationCanceledException || e is IOException)
			{
				//	Serial.Close();
				//	Dispatcher.UIThread.Post(() => ConnectButton.Content = "接続");
				//	UpdateCover(true, (e is TimeoutException) ? "受信できません" : "切断しました");
				//	return;
			}
			finally
			{
				Serial.Close();
				Dispatcher.UIThread.Post(() => ConnectButton.Content = "接続");
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
}
