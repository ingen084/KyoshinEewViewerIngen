using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Exceptions;
using KyoshinMonitorLib;
using R3;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Series.Qzss.Services;

public partial class SerialConnector : ObservableObject
{
	private bool isConnected;
	public bool IsConnected
	{
		get => isConnected;
		private set => SetProperty(ref isConnected, value);
	}

	[ObservableProperty]
	public partial Location? CurrentLocation { get; set; }

	[ObservableProperty]
	public partial DateTime? LastReceivedTime { get; set; }

	[ObservableProperty]
	public partial float? Direction { get; set; }

	[ObservableProperty]
	public partial float? SpeedKiloMeterPerHour { get; set; }

	[ObservableProperty]
	public partial string? GpsMode { get; set; }

	public event Action<DCReport>? DCReportReceived;

	private bool IsClosing { get; set; }
	private Task ReceiveTask { get; }

	private KyoshinEewViewerConfiguration Config { get; }

	private ILogger Logger { get; }

	public SerialConnector(ILogger<SerialConnector> logger, KyoshinEewViewerConfiguration config)
	{
		Logger = logger;
		StrongReferenceMessenger.Default.Register<ApplicationClosing>(this, (_, s) => IsClosing = true);
		Config = config;
		ReceiveTask = Task.Run(Receive, CancellationToken.None);
	}

	private SerialPort? CurrentPort { get; set; }
	private void Receive()
	{
		var buffer = new byte[1024];
		var parser = new NmeaUbxFrameParser();
		parser.SentenceDetected += OnSentenceDetected;

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
					parser.Reset();
					IsConnected = true;
					using (Config.Qzss.ObservePropertyChanged(x => x.Connect).Where(c => !c).Subscribe(x => CurrentPort.Close()))
					{
						Logger.LogInformation("{SerialPort} をオープンしました", Config.Qzss.SerialPort);

						while (!IsClosing)
						{
							var count = CurrentPort.Read(buffer, 0, buffer.Length);
							parser.Feed(buffer.AsSpan(0, count));
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

	// 完成したセンテンスを受け取り、種別ごとに処理する
	private void OnSentenceDetected(NmeaUbxFrameParser.SentenceType type, ReadOnlySpan<byte> payload)
	{
		switch (type)
		{
			case NmeaUbxFrameParser.SentenceType.Nmea:
				ProcessNmeaSentence(Encoding.ASCII.GetString(payload));
				break;
			case NmeaUbxFrameParser.SentenceType.Ubx:
				ProcessUbxSentence(payload);
				break;
		}
	}

	// 完成した NMEA センテンスを処理する (チェックサム検証はパーサー側で実施済み)
	private void ProcessNmeaSentence(string nmea)
	{
		var csIndex = nmea.IndexOf('*');
		if (csIndex == -1)
			return;

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

	// 完成した UBX センテンスを処理する (チェックサム検証はパーサー側で実施済み)
	private void ProcessUbxSentence(ReadOnlySpan<byte> sentence)
	{
		// payload length を読み出す
		if (sentence.Length < 8)
			return;
		var ubxLength = BitConverter.ToUInt16(sentence[4..6]);

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
	private sealed partial class AckWaiter(byte clsId, byte msgId)
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
		Logger.LogInformation("ボーレート {NewBaudRate} で再接続します", newBaudRate);
		Config.Qzss.Connect = false;
		await Task.Delay(500);
		Config.Qzss.BaudRate = newBaudRate;
		Config.Qzss.Connect = true;
	}

	/// <summary>
	/// 指定したボーレートのリストを順に試し、最初に有効な NMEA / UBX センテンスを受信できたレートを返す。
	/// 検出中は通常の Receive ループとは独立に一時的なシリアルポートを開閉する。
	/// Config.Qzss.BaudRate は本メソッド内では一切変更しない。
	/// </summary>
	/// <param name="portName">対象ポート名</param>
	/// <param name="baudRates">試行するボーレートの順序付きリスト</param>
	/// <param name="perRateTimeoutMs">1 レートあたりの待機時間 (ミリ秒)</param>
	/// <param name="onProgress">現在試行中のボーレートを通知するコールバック (UI 表示用)</param>
	/// <param name="cancellationToken">キャンセル用トークン</param>
	/// <returns>検出に成功したボーレート。失敗時は null</returns>
	public async Task<int?> DetectBaudRateAsync(
		string portName,
		int[] baudRates,
		int perRateTimeoutMs,
		Action<int>? onProgress,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(portName))
			throw new ArgumentException("ポート名が指定されていません", nameof(portName));
		if (baudRates is null || baudRates.Length == 0)
			throw new ArgumentException("ボーレートが指定されていません", nameof(baudRates));

		foreach (var baudRate in baudRates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			onProgress?.Invoke(baudRate);

			// バックグラウンドスレッドで同期的に試行する (SerialPort.Read は同期 API のため)
			var detected = await Task.Run(() => TryDetectAtBaudRate(portName, baudRate, perRateTimeoutMs, cancellationToken), cancellationToken).ConfigureAwait(false);
			if (detected)
			{
				Logger.LogInformation("ボーレート {BaudRate} で受信を確認しました", baudRate);
				return baudRate;
			}
		}

		return null;
	}

	private bool TryDetectAtBaudRate(string portName, int baudRate, int timeoutMs, CancellationToken cancellationToken)
	{
		Logger.LogDebug("ボーレート {BaudRate} を試行中", baudRate);

		using var port = new SerialPort(portName)
		{
			BaudRate = baudRate,
			// Read のキャンセル応答性確保のため短めにする
			ReadTimeout = 200,
		};

		try
		{
			port.Open();
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "ボーレート {BaudRate} でポートを開けませんでした", baudRate);
			return false;
		}

		try
		{
			// ボーレートを切り替えた直後はガベージが混入しやすいため破棄する
			try { port.DiscardInBuffer(); } catch { }

			var parser = new NmeaUbxFrameParser();
			var detected = false;
			parser.SentenceDetected += (_, _) => detected = true;

			var buffer = new byte[1024];
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int count;
				try
				{
					count = port.Read(buffer, 0, buffer.Length);
				}
				catch (TimeoutException)
				{
					continue;
				}
				if (count <= 0)
					continue;
				parser.Feed(buffer.AsSpan(0, count));
				if (detected)
					return true;
			}
			return false;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "ボーレート {BaudRate} の検出中にエラーが発生しました", baudRate);
			return false;
		}
		finally
		{
			try { port.Close(); } catch { }
		}
	}
}
