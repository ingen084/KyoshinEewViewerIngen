using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace KyoshinEewViewer.Series.Radar;

public class RadarImagePuller
{
	// 気象庁にリクエストを投げるスレッド数
	// ブラウザは基本6だがXMLの取得などもあるので5
	private const int PullImageThreadCount = 5;

	private HttpClient Client { get; }
	private ILogger Logger { get; }

	private Thread[]? PullImageThreads { get; set; }
	private ConcurrentQueue<(RadarImageTileProvider sender, (int z, int x, int y) loc, string url)> PullImageQueue { get; } = new();
	private List<string> WorkingUrls { get; } = [];
	private bool IsShutdown { get; set; }
	private ManualResetEventSlim SleepEvent { get; } = new(false);

	public RadarImagePuller(ILogManager logManager, HttpClient client, InformationCacheService cacheService)
	{
		Client = client;
		Logger = logManager.GetLogger<RadarImagePuller>();

		PullImageThreads = new Thread[PullImageThreadCount];
		for (var i = 0; i < PullImageThreadCount; i++)
		{
			var threadNumber = i;
			PullImageThreads[i] = new Thread(async s =>
			{
				Debug.WriteLine($"thread {threadNumber} started");
				while (!IsShutdown)
				{
					if (PullImageQueue.IsEmpty)
					{
						SleepEvent.Reset();
						SleepEvent.Wait();
						continue;
					}

					if (!PullImageQueue.TryDequeue(out var data))
						continue;
					lock (WorkingUrls)
					{
						if (WorkingUrls.Contains(data.url))
							continue;
						WorkingUrls.Add(data.url);
					}
					if (data.sender.IsDisposed)
						continue;
					try
					{
						//Debug.WriteLine($"{DateTime.Now:ss.FFF} thread{threadNumber} pulling {data.url}");
						try
						{
							var sw = Stopwatch.StartNew();
							data.sender.OnImageUpdated(
								data.loc,
								await cacheService.TryGetOrFetchImageAsync(
									data.url,
									async () =>
									{
										using var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, data.url));
										if (!response.IsSuccessStatusCode)
											throw new Exception($"タイル画像の取得に失敗しました({response.StatusCode}) " + data.url);
										var bitmap = SKBitmap.Decode(await response.Content.ReadAsStreamAsync());
										//unsafe
										//{
										//	var ptr = (uint*)bitmap.GetPixels().ToPointer();
										//	var pixelCount = bitmap.Width * bitmap.Height;
										//	// 透過画像に加工する
										//	for (var i = 0; i < pixelCount; i++)
										//		*ptr++ &= 0xDD_FF_FF_FF;
										//}
										//bitmap.NotifyPixelsChanged();
										return (bitmap, DateTime.Now.AddHours(3));
									}));
							if (sw.ElapsedMilliseconds > 0)
								Debug.WriteLine($"{DateTime.Now:ss.FFF} pulled {sw.Elapsed.TotalMilliseconds:0.00}ms thread{threadNumber} {data.url}");
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex, "タイル画像の取得に失敗");
						}
					}
					finally
					{
						lock (WorkingUrls)
							if (WorkingUrls.Contains(data.url))
								WorkingUrls.Remove(data.url);
					}
				}
			});
			PullImageThreads[i].Start();
		}
	}

	public void FetchImage(RadarImageTileProvider sender, (int z, int x, int y) loc, string url)
	{
		if (PullImageQueue.Contains((sender, loc, url)))
			return;
		PullImageQueue.Enqueue((sender, loc, url));
		SleepEvent.Set();
	}

	public void Cleanup()
	{
		PullImageQueue.Clear();
		lock (WorkingUrls)
			WorkingUrls.Clear();
	}

	public void Shutdown()
	{
		IsShutdown = true;
		SleepEvent.Set();
	}
}
