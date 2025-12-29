using KyoshinMonitorLib.Timers;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ShakeDetectionProducer.Services;

/// <summary>
/// NTP時刻同期と秒ベースタイマーを提供するシンプルなサービス
/// </summary>
public class SimpleTimerService : IDisposable
{
	private ILogger<SimpleTimerService> Logger { get; }
	private SecondBasedTimer Timer { get; }
	private bool _disposed;

	/// <summary>
	/// 現在時刻（タイマーベース）
	/// </summary>
	public DateTime CurrentTime => LastElapsedTime + (DateTime.Now - LastElapsedLocalTime);

	private DateTime LastElapsedTime { get; set; }
	private DateTime LastElapsedLocalTime { get; set; }

	/// <summary>
	/// 毎秒発火するイベント
	/// </summary>
	public event Func<DateTime, Task>? TimerElapsed;

	public SimpleTimerService(ILogger<SimpleTimerService> logger)
	{
		Logger = logger;

		Timer = new SecondBasedTimer
		{
			Offset = TimeSpan.Zero,
			Accuracy = TimeSpan.FromMilliseconds(100),
			BlockingMode = false,
		};

		Timer.Elapsed += async time =>
		{
			LastElapsedTime = time;
			LastElapsedLocalTime = DateTime.Now;

			if (TimerElapsed != null)
				await TimerElapsed.Invoke(time);
		};
	}

	/// <summary>
	/// タイマーを開始する
	/// </summary>
	/// <param name="offsetMs">オフセット（ミリ秒）</param>
	public void Start(int offsetMs = 1100)
	{
		Logger.LogInformation("初回の時刻同期・タイマーを開始します");

		var time = GetNetworkTime();
		if (time == null)
		{
			Logger.LogWarning("NTP同期に失敗したため、ローカル時刻を使用します");
			time = DateTime.UtcNow.AddHours(9);
		}

		// オフセット分遅延させる
		Timer.Offset = TimeSpan.FromMilliseconds(offsetMs);
		Timer.Start(time.Value);

		Logger.LogInformation("タイマーを開始しました: {Time:yyyy/MM/dd HH:mm:ss.fff}", time.Value);
	}

	/// <summary>
	/// NTPサーバーから時刻を取得する
	/// </summary>
	public DateTime? GetNetworkTime(string host = "ntp.nict.jp", ushort port = 123, int timeout = 1000)
	{
		for (var attempt = 0; attempt < 10; attempt++)
		{
			try
			{
				// RFC 2030準拠
				var ntpData = new byte[48];
				ntpData[0] = 0b00_100_011; // LI=0, VN=4, Mode=3

				if (!IPAddress.TryParse(host, out var addr))
				{
					var addresses = Dns.GetHostEntry(host, AddressFamily.InterNetwork).AddressList;
					addr = addresses[Random.Shared.Next(addresses.Length)];
				}

				DateTime sendedTime, receivedTime;
				var endPoint = new IPEndPoint(addr, port);
				using (var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
				{
					socket.Connect(endPoint);
					socket.ReceiveTimeout = timeout;

					sendedTime = DateTime.Now;
					socket.Send(ntpData);
					socket.Receive(ntpData);
					receivedTime = DateTime.Now;
				}

				var serverReceivedTime = ToTime(ntpData, 32);
				var serverSendedTime = ToTime(ntpData, 40);

				var delta = TimeSpan.FromTicks(
					(receivedTime.Ticks - sendedTime.Ticks - (serverSendedTime.Ticks - serverReceivedTime.Ticks)) / 2);

				Logger.LogDebug("NTP同期完了 - 往復時間: {RoundTrip}, サーバー処理時間: {ServerProcessing}, 片道推定: {Delta}",
					receivedTime - sendedTime,
					serverSendedTime - serverReceivedTime,
					delta);

				return serverSendedTime + delta;
			}
			catch (SocketException ex)
			{
				Logger.LogWarning(ex, "NTP同期試行 {Attempt}/10 が失敗しました", attempt + 1);
			}
		}

		return null;
	}

	private static DateTime ToTime(byte[] bytes, ushort offset)
	{
		ulong intPart = SwapEndianness(BitConverter.ToUInt32(bytes, offset));
		ulong fractPart = SwapEndianness(BitConverter.ToUInt32(bytes, offset + 4));

		var milliseconds = (intPart * 1000) + (fractPart * 1000 / 0x100000000L);

		return new DateTime(1900, 1, 1, 9, 0, 0).AddMilliseconds((long)milliseconds);
	}

	private static uint SwapEndianness(ulong x)
		=> (uint)(((x & 0x000000ff) << 24) +
				  ((x & 0x0000ff00) << 8) +
				  ((x & 0x00ff0000) >> 8) +
				  ((x & 0xff000000) >> 24));

	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;

		Timer.Stop();
		GC.SuppressFinalize(this);
	}
}
