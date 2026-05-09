using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Exceptions;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Qzss.Services;

public class SerialConnector : ReactiveObject
{
	private bool isConnected;
	public bool IsConnected
	{
		get => isConnected;
		private set => this.RaiseAndSetIfChanged(ref isConnected, value);
	}

	private Location? _currentLocation;
	public Location? CurrentLocation
	{
		get => _currentLocation;
		set => this.RaiseAndSetIfChanged(ref _currentLocation, value);
	}

	private DateTime? _lastReceivedTime;
	public DateTime? LastReceivedTime
	{
		get => _lastReceivedTime;
		set => this.RaiseAndSetIfChanged(ref _lastReceivedTime, value);
	}

	private float? _direction;
	public float? Direction
	{
		get => _direction;
		set => this.RaiseAndSetIfChanged(ref _direction, value);
	}

	private float? _speedKiloMeterPerHour;
	public float? SpeedKiloMeterPerHour
	{
		get => _speedKiloMeterPerHour;
		set => this.RaiseAndSetIfChanged(ref _speedKiloMeterPerHour, value);
	}

	private string? _gpsMode;
	public string? GpsMode
	{
		get => _gpsMode;
		set => this.RaiseAndSetIfChanged(ref _gpsMode, value);
	}

	public event Action<DCReport>? DCReportReceived;

	private bool IsClosing { get; set; }
	private Task ReceiveTask { get; }

	private KyoshinEewViewerConfiguration Config { get; }

	private ILogger Logger { get; }

	public SerialConnector(ILogManager logManager, KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<SerialConnector>();

		Logger = logManager.GetLogger<SerialConnector>();
		MessageBus.Current.Listen<ApplicationClosing>().Subscribe(s => IsClosing = true);
		Config = config;
		ReceiveTask = Task.Run(Receive, CancellationToken.None);
	}

	private SerialPort? CurrentPort { get; set; }
	private void Receive()
	{
		var buffer = new byte[1024];
		while (!IsClosing)
		{
			if (string.IsNullOrWhiteSpace(Config.Qzss.SerialPort) || !Config.Qzss.Connect)
			{
				Thread.Sleep(1000);
				continue;
			}
			using (CurrentPort = new SerialPort(Config.Qzss.SerialPort)
			{
				BaudRate = Config.Qzss.BaudRate,
				ReadTimeout = 10000,
			})
			{
				try
				{
					CurrentPort.Open();
					IsConnected = true;
					using (Config.Qzss.WhenAnyValue(x => x.Connect).Where(c => !c).Subscribe(x => CurrentPort.Close()))
					{
						var type = SentenceType.None;
						ushort ubxLength = 0;
						var sentenceIndex = 0;
						var sentence = new byte[1024];

						Logger.LogInfo($"{Config.Qzss.SerialPort} をオープンしました");

						while (!IsClosing)
						{
							var count = CurrentPort.Read(buffer, 0, buffer.Length);
							for (var i = 0; i < count; i++)
							{
								var c = buffer[i];

								switch (type)
								{
									// センテンスの開始を探す
									case SentenceType.None:
										switch (c)
										{
											// NMEA
											case (byte)'$':
												type = SentenceType.Nmea;
												sentence[0] = c;
												sentenceIndex = 1;
												continue;
											// UBX
											case 0xb5:
												sentence[0] = c;
												sentenceIndex = 1;
												continue;
											case 0x62 when sentenceIndex == 1 && sentence[0] == 0xb5:
												type = SentenceType.Ubx;
												sentence[sentenceIndex++] = c;
												continue;
											default:
												continue;
										}
									case SentenceType.Nmea:
										{
											sentence[sentenceIndex++] = c;
											if (c == '\n' && sentence[sentenceIndex - 2] == '\r')
											{
												ProcessNmeaSentence(Encoding.ASCII.GetString(sentence.AsSpan(0, sentenceIndex)));
												type = SentenceType.None;
											}

											break;
										}
									case SentenceType.Ubx:
										{
											sentence[sentenceIndex++] = c;
											// payload length を読む
											if (sentenceIndex == 6)
												ubxLength = BitConverter.ToUInt16(sentence, 4);
											else if (sentenceIndex > 6 && sentenceIndex >= ubxLength + 6 + 2)
											{
												// UBX センテンスの完成
												ProcessUbxSentence(sentence.AsSpan(0, sentenceIndex), ubxLength);
												type = SentenceType.None;
											}
											break;
										}
								}
							}
						}
					}
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "Serial Error");
				}
				finally
				{
					IsConnected = false;
					Thread.Sleep(1000);
				}
			}
		}
	}

	// 完成した NMEA センテンスを処理する
	private void ProcessNmeaSentence(string nmea)
	{
		// チェックサム確認
		var csIndex = nmea.IndexOf('*');
		// チェックサムがないセンテンスは無視する
		if (csIndex == -1)
			return;

		// チェックサムを取得
		var chS = nmea[(csIndex + 1)..].TrimEnd('\r', '\n');
		byte checkSum = 0;
		foreach (var b in nmea[1..csIndex])
			checkSum ^= (byte)b;
		if (chS != checkSum.ToString("X2"))
		{
			Logger.LogInfo("NMEA チェックサム エラー: " + nmea[1..csIndex]);
			return;
		}

		var parts = nmea[1..csIndex].Split(',');
		if (parts.Length < 2 || parts[0].Length != 5)
			return;

		// 汎用情報
		if (parts[0].EndsWith("RMC"))
		{
			if (parts.Length < 10)
				return;
			// 時刻
			if (parts[1].Length >= 8 &&
				int.TryParse(parts[1][..2], out var hour) &&
				int.TryParse(parts[1][2..4], out var minute) &&
				int.TryParse(parts[1][4..6], out var second) &&
				int.TryParse(parts[1][7..], out var centisecond) &&

				parts[9].Length >= 6 &&
				int.TryParse(parts[9][..2], out var day) &&
				int.TryParse(parts[9][2..4], out var month) &&
				int.TryParse(parts[9][4..], out var year)
			)
				LastReceivedTime = new DateTime(2000 + year, month, day, hour, minute, second, centisecond * 10).AddHours(-Config.Qzss.TimezoneOffset);
			else
				LastReceivedTime = null;

			// 座標
			if (
				parts[3].Length >= 10 &&
				parts[5].Length >= 11 &&
				int.TryParse(parts[3][..2], out var latDeg) &&
				double.TryParse(parts[3][2..], out var latFra) &&
				int.TryParse(parts[5][..3], out var lonDeg) &&
				double.TryParse(parts[5][3..], out var lonFra)
			)
				CurrentLocation = new Location((float)((latDeg + latFra / 60) * (parts[4] == "S" ? -1 : 1)), (float)((lonDeg + lonFra / 60) * (parts[6] == "W" ? -1 : 1)));
			else
				CurrentLocation = null;

			// 速度
			if (parts[7].Length >= 5 && float.TryParse(parts[7], out var speed))
				SpeedKiloMeterPerHour = speed * 1.852f;
			else
				SpeedKiloMeterPerHour = null;

			// 方位
			if (parts[8].Length >= 5 && float.TryParse(parts[8], out var direction))
				Direction = direction;
			else
				Direction = null;

			// GPS モード
			if (parts[12].Length >= 1)
				GpsMode = parts[12];
			else
				GpsMode = null;
		}

		// QZQSM
		else if (parts[0] == "QZQSM")
		{
			try
			{
				DCReportReceived?.Invoke(DCReport.ParseFromNmea(nmea));
			}
			catch (DCReportParseException e)
			{
				Logger.LogError(e, "NMEA DCReport Error");
			}
		}
	}

	// 完成した UBX センテンスを処理する
	private void ProcessUbxSentence(Span<byte> sentence, ushort ubxLength)
	{
		byte csA = 0;
		byte csB = 0;
		for (var j = 2; j < sentence.Length - 2; j++)
		{
			csA = (byte)(csA + sentence[j]);
			csB = (byte)(csB + csA);
		}
		if (csA != sentence[^2] || csB != sentence[^1])
		{
			Logger.LogInfo($"UBX チェックサム　エラー: {csA:X2} {sentence[^2]:X2} {csB:X2} {sentence[^1]:X2}");
			return;
		}

		// UBX-ACK-ACK(0x05/0x01) / UBX-ACK-NAK(0x05/0x00) -- payload[0]=clsId, payload[1]=msgId
		if (sentence[2] == 0x05 && (sentence[3] == 0x01 || sentence[3] == 0x00) && ubxLength >= 2 &&
			Volatile.Read(ref _ackWaiter) is { } w && w.ClsId == sentence[6] && w.MsgId == sentence[7])
			w.Tcs.TrySetResult(sentence[3] == 0x01);

		// UBX-RXM-SFRBX, 40 bytes, QZSS L1S (災危通報)
		// sentence[8] (sigId): 0 = L1C/A (航法メッセージ), 1 = L1S (災危通報)
		if (sentence[2] == 2 && sentence[3] == 0x13 && ubxLength >= 40 && sentence[6] == 5 && sentence[8] == 1 && sentence[10] >= 8)
		{
			var data = new byte[sentence[10] * 4];
			for (var j = 0; j < sentence[10]; j++)
			{
				data[j * 4 + 0] = sentence[14 + j * 4 + 3];
				data[j * 4 + 1] = sentence[14 + j * 4 + 2];
				data[j * 4 + 2] = sentence[14 + j * 4 + 1];
				data[j * 4 + 3] = sentence[14 + j * 4 + 0];
			}

			if (data.Length < 32)
				return;
			try
			{
				DCReportReceived?.Invoke(DCReport.Parse(data[..32]));
			}
			catch (DCReportParseException e)
			{
				Logger.LogWarning(e, "UBX DCReport Error");
			}
		}
	}

	private AckWaiter? _ackWaiter;

	// UBX-ACK 待機のための情報を一括で保持する
	private sealed class AckWaiter(byte clsId, byte msgId)
	{
		public byte ClsId { get; } = clsId;
		public byte MsgId { get; } = msgId;
		public TaskCompletionSource<bool> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	// 指定したメッセージを送信し、対応する UBX-ACK の到着を待機する
	// 戻り値: true=ACK, false=NAK or タイムアウト
	private async Task<bool> SendAndWaitAckAsync(byte clsId, byte msgId, byte[] data, int timeoutMs)
	{
		var port = CurrentPort ?? throw new InvalidOperationException("ポートが開いていません。接続してから再度お試しください。");
		var waiter = new AckWaiter(clsId, msgId);
		Interlocked.Exchange(ref _ackWaiter, waiter);
		try
		{
			port.Write(data, 0, data.Length);
			return await Task.WhenAny(waiter.Tcs.Task, Task.Delay(timeoutMs)) == waiter.Tcs.Task && waiter.Tcs.Task.Result;
		}
		finally
		{
			Interlocked.CompareExchange(ref _ackWaiter, null, waiter);
		}
	}

	/// <summary>
	/// UBX-CFG-VALSET メッセージを構築する (RAM レイヤーのみ)
	/// </summary>
	private static byte[] BuildCfgValSet(uint key, ReadOnlySpan<byte> value)
	{
		var payloadLen = 4 + 4 + value.Length;
		var buf = new byte[6 + payloadLen + 2];
		buf[0] = 0xB5;
		buf[1] = 0x62;
		buf[2] = 0x06; // CFG
		buf[3] = 0x8A; // VALSET
		buf[4] = (byte)(payloadLen & 0xFF);
		buf[5] = (byte)(payloadLen >> 8);
		buf[6] = 0x01; // version
		buf[7] = 0x01; // layers (RAM only)
		buf[8] = 0;
		buf[9] = 0;
		buf[10] = (byte)(key & 0xFF);
		buf[11] = (byte)((key >> 8) & 0xFF);
		buf[12] = (byte)((key >> 16) & 0xFF);
		buf[13] = (byte)((key >> 24) & 0xFF);
		value.CopyTo(buf.AsSpan(14, value.Length));

		byte csA = 0, csB = 0;
		for (var j = 2; j < buf.Length - 2; j++)
		{
			csA = (byte)(csA + buf[j]);
			csB = (byte)(csB + csA);
		}
		buf[^2] = csA;
		buf[^1] = csB;
		return buf;
	}

	/// <summary>
	/// UBX-CFG-VALSET を送信する
	/// </summary>
	/// <param name="key">設定キー</param>
	/// <param name="value">値(リトルエンディアン)</param>
	/// <param name="waitAck">ACK を待機するか (false の場合は送信のみ)</param>
	/// <param name="timeoutMs">ACK 待機タイムアウト</param>
	/// <returns>waitAck=true: true=ACK / false=NAK or タイムアウト, waitAck=false: 常に true</returns>
	/// <exception cref="InvalidOperationException">ポートが開いていない場合</exception>
	public Task<bool> SendCfgValSetAsync(uint key, byte[] value, bool waitAck = true, int timeoutMs = 2000)
	{
		var data = BuildCfgValSet(key, value);
		if (waitAck)
			return SendAndWaitAckAsync(0x06, 0x8A, data, timeoutMs);

		var port = CurrentPort ?? throw new InvalidOperationException("ポートが開いていません。接続してから再度お試しください。");
		port.Write(data, 0, data.Length);
		return Task.FromResult(true);
	}

	/// <summary>
	/// 接続を切断し、新しいボーレートで再接続する
	/// </summary>
	public async Task ReconnectWithBaudRateAsync(int newBaudRate)
	{
		Logger.LogInfo($"ボーレート {newBaudRate} で再接続します");
		Config.Qzss.Connect = false;
		await Task.Delay(500);
		Config.Qzss.BaudRate = newBaudRate;
		Config.Qzss.Connect = true;
	}

	public enum SentenceType
	{
		None,
		Nmea,
		Ubx,
	}
}
