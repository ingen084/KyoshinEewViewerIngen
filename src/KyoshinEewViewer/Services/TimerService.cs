using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinMonitorLib.Timers;
using Splat;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;

public partial class TimerService
{
	[GeneratedRegex("[^0-9]*(\\d+\\.\\d+)+.*", RegexOptions.Compiled)]

	private static partial Regex TimeRegex();

	private HttpClient HttpClient { get; }

	/// <summary>
	/// 時刻同期など定期的なタスクを行うタイマー
	/// </summary>
	public Timer RegularlyTimer { get; }
	/// <summary>
	/// 正確な日本標準時を刻むだけのタイマー
	/// </summary>
	private SecondBasedTimer MainTimer { get; }
	private ILogger Logger { get; }
	/// <summary>
	/// 遅延タイマーが発行する時刻
	/// </summary>
	private DateTime DelayedTime { get; set; }
	/// <summary>
	/// 遅延タイマー 1秒未満のオフセットを処理する
	/// </summary>
	private Timer DelayedTimer { get; }
	/// <summary>
	/// 遅延タイマーが動作中かどうか
	/// </summary>
	private bool IsDelayedTimerRunning { get; set; }

	private KyoshinEewViewerConfiguration Config { get; }

	/// <summary>
	/// 遅延タイマーから計算される現在時刻
	/// </summary>
	public DateTime CurrentDisplayTime => LastElapsedDelayedTime + (DateTime.Now - LastElapsedDelayedLocalTime);

	private DateTime LastElapsedDelayedTime { get; set; }
	private DateTime LastElapsedDelayedLocalTime { get; set; }

	/// <summary>
	/// メインタイマーから計算される現在時刻
	/// </summary>
	public DateTime CurrentTime => LastElapsedTime + (DateTime.Now - LastElapsedLocalTime);

	private DateTime LastElapsedTime { get; set; }
	private DateTime LastElapsedLocalTime { get; set; }

	public event Action<DateTime>? TimerElapsed;
	public event Action<DateTime>? DelayedTimerElapsed;

	public TimerService(ILogManager logManager, KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<TimerService>();

		Config = config;
		Logger = logManager.GetLogger<TimerService>();
		HttpClient = new() { Timeout = TimeSpan.FromMilliseconds(1000) };
		HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("UserAgent", "KEViFallback");

		RegularlyTimer = new Timer(s =>
		{
			var nullableTime = GetNowTime();
			if (nullableTime is { } time)
			{
				Logger.LogDebug($"時刻同期を行いました {time:yyyy/MM/dd HH:mm:ss.fff}");
				MainTimer?.UpdateTime(time);
			}
		}, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

		MainTimer = new SecondBasedTimer()
		{
			Offset = TimeSpan.Zero,//TimeSpan.FromMilliseconds(ConfigService.Configuration.Timer.Offset),
			Accuracy = TimeSpan.FromMilliseconds(100),
			BlockingMode = false,
		};

		DelayedTimer = new Timer(s =>
		{
			if (IsDelayedTimerRunning)
				return;

			IsDelayedTimerRunning = true;
			DelayedTimerElapsed?.Invoke(DelayedTime);
			LastElapsedDelayedTime = DelayedTime;
			LastElapsedDelayedLocalTime = DateTime.Now;
			IsDelayedTimerRunning = false;
		}, null, Timeout.Infinite, Timeout.Infinite);

		MainTimer.Elapsed += t =>
		{
			var delay = TimeSpan.FromMilliseconds(Config.Timer.Offset);
			DelayedTime = t.AddSeconds(-(int)delay.TotalSeconds);
			DelayedTimer.Change(TimeSpan.FromSeconds(delay.TotalSeconds % 1), Timeout.InfiniteTimeSpan);

			LastElapsedTime = t;
			LastElapsedLocalTime = DateTime.Now;

			TimerElapsed?.Invoke(t);
			return Task.CompletedTask;
		};
	}

	public bool Started { get; private set; }
	public void StartMainTimer()
	{
		if (Started)
			return;
		Started = true;
		Logger.LogInfo("初回の時刻同期･メインタイマーを開始します。");
		var time = GetNowTime() ?? DateTime.UtcNow.AddHours(9);
		MainTimer.Start(time);
		Logger.LogInfo("メインタイマーを開始しました。");
	}

	public DateTime? GetNowTime()
	{
		try
		{
			if (!Config.NetworkTime.Enable)
				return DateTime.UtcNow.AddHours(9);
			
			var count = 0;
			while (true)
			{
				count++;
				var time = GetNetworkTimeWithNtp(Config.NetworkTime.Address);
				if (time != null)
				{
					Logger.LogDebug($"時刻同期結果: {time:yyyy/MM/dd HH:mm:ss.fff}");
					return time;
				}
				if (count >= 10)
					throw new Exception("リトライ回数が上限を超えました");
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "NTPによる時刻同期に失敗");
		}

		if (!Config.NetworkTime.EnableFallbackHttp)
			return null;

		try
		{
			var sw = Stopwatch.StartNew();
			var match = TimeRegex().Match(HttpClient.GetStringAsync("https://svs.ingen084.net/time/").Result);
			sw.Stop();
			var dt = new DateTime(1970, 1, 1, 9, 0, 0).AddSeconds(double.Parse(match.Groups[1].Value));
			dt += sw.Elapsed / 2; // 取得時間/2 を足すことによって通信のラグを考慮する
			return dt;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "HTTPによる時刻同期に失敗");
		}
		return null;
	}

	/// <summary>
	/// Ntp通信を使用してネットワーク上から時刻を取得します。
	/// </summary>
	/// <param name="hostName">ホスト名</param>
	/// <param name="port">ポート番号 通常はデフォルトのままで構いません。</param>
	/// <param name="timeout">タイムアウト時間(ミリ秒)</param>
	/// <returns>取得された時刻 取得に失敗した場合はnullが返されます。</returns>
	public DateTime? GetNetworkTimeWithNtp(string hostName = "ntp.nict.jp", ushort port = 123, int timeout = 1000)
	{
		try
		{
			// RFC 2030準拠
			var ntpData = new byte[48];

			//特に使用しません
			ntpData[0] = 0b00_100_011;//うるう秒指定子 = 0 (警告なし), バージョン = 4 (SNTP), Mode = 3 (クライアント)


			if (!IPAddress.TryParse(hostName, out var addr))
			{
				var addresses = Dns.GetHostEntry(hostName, AddressFamily.InterNetwork).AddressList;
				addr = addresses[new Random().Next(addresses.Length)];
			}

			DateTime sendedTime, recivedTime;
			var endPoint = new IPEndPoint(addr, port);
			using (var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
			{
				socket.Connect(endPoint);
				socket.ReceiveTimeout = timeout;

				sendedTime = DateTime.Now;
				socket.Send(ntpData);

				socket.Receive(ntpData);
				recivedTime = DateTime.Now;
			}

			//受信時刻=32 送信時刻=40
			var serverReceivedTime = ToTime(ntpData, 32);
			var serverSendedTime = ToTime(ntpData, 40);

			// (送信から受信までの時間 - 鯖側での受信から送信までの時間) / 2
			var delta = TimeSpan.FromTicks((recivedTime.Ticks - sendedTime.Ticks - (serverSendedTime.Ticks - serverReceivedTime.Ticks)) / 2);
			Logger.LogDebug($"同期時間: {recivedTime - sendedTime} サーバー内処理時間: {serverSendedTime - serverReceivedTime} 片道の通信時間: {delta}");
			return serverSendedTime + delta;
		}
		catch (SocketException ex)
		{
			Logger.LogWarning(ex, "socket exception");
			return null;
		}
	}

	private static DateTime ToTime(byte[] bytes, ushort offset)
	{
		ulong intPart = SwapEndianness(BitConverter.ToUInt32(bytes, offset));
		ulong fractPart = SwapEndianness(BitConverter.ToUInt32(bytes, offset + 4));

		var milliseconds = (intPart * 1000) + (fractPart * 1000 / 0x100000000L);

		//時間生成
		return new DateTime(1900, 1, 1, 9, 0, 0).AddMilliseconds((long)milliseconds);
	}

	//ビット列を逆にする stackoverflow.com/a/3294698/162671
	internal static uint SwapEndianness(ulong x)
		=> (uint)(((x & 0x000000ff) << 24) +
				  ((x & 0x0000ff00) << 8) +
				  ((x & 0x00ff0000) >> 8) +
				  ((x & 0xff000000) >> 24));
}
