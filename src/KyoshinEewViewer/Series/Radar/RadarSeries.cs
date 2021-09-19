using Avalonia.Controls;
using KyoshinEewViewer.Series.Radar.RenderObjects;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace KyoshinEewViewer.Series.Radar
{
	public class RadarSeries : SeriesBase
	{
		public static HttpClient Client { get; } = new();
		private ILogger Logger { get; }

		[Reactive]
		public DateTime CurrentDateTime { get; set; } = DateTime.Now;
		[Reactive]
		public bool IsLoading { get; set; } = true;

		private int timeSliderValue;
		public int TimeSliderValue
		{
			get => timeSliderValue;
			set {
				if (timeSliderValue == value)
					return;
				this.RaiseAndSetIfChanged(ref timeSliderValue, value);
				UpdateTiles();
			}
		}
		[Reactive]
		public int TimeSliderSize { get; set; } = 1;

		[Reactive]
		public JmaRadarTime[]? JmaRadarTimes { get; set; }

		// 気象庁にリクエストを投げるスレッド数
		// ブラウザは基本6だがXMLの取得などもあるので5
		private const int PullImageThreadCount = 5;
		private Thread[] PullImageThreads { get; }
		private ConcurrentQueue<(RadarImageTileProvider sender, (int z, int x, int y) loc, string url)> PullImageQueue { get; } = new();
		private List<string> WorkingUrls { get; } = new();
		private bool IsShutdown { get; set; }
		private ManualResetEventSlim SleepEvent { get; } = new(false);
		public RadarSeries() : base("雨雲レーダーβ")
		{
			Logger = LoggingService.CreateLogger(this);
			MapPadding = new Avalonia.Thickness(0, 50, 0, 0);
			Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Assembly.GetExecutingAssembly().GetName().Version};twitter@ingen084");

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
									await InformationCacheService.TryGetOrFetchImageAsync(
										data.url,
										async () =>
										{
											using var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, data.url));
											if (!response.IsSuccessStatusCode)
												throw new Exception("タイル画像の取得に失敗しました " + data.url);
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
								Logger.LogWarning("タイル画像の取得に失敗: {ex}", ex);
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

		private RadarView? control;
		public override Control DisplayControl => control ?? throw new Exception("初期化前にコントロールが呼ばれています");

		public override void Activating()
		{
			if (control != null)
				return;
			control = new RadarView
			{
				DataContext = this,
			};
			Reload(true);
			TimerService.Default.TimerElapsed += t =>
			{
				if (t.Second != 20)
					return;
				// 自動更新が有効であれば更新を そうでなければキャッシュの揮発を行う
				if (ConfigurationService.Current.Radar.AutoUpdate)
					Reload(false);
				else
					UpdateTiles();
			};
			TimerService.Default.StartMainTimer();
		}
		public async void Reload(bool init = false)
		{
			IsLoading = true;

			using var response = await Client.GetAsync("https://www.jma.go.jp/bosai/jmatile/data/nowc/targetTimes_N1.json");
			if (!response.IsSuccessStatusCode)
			{
				IsLoading = false;
				return;
			}
			var baseTimes = (await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await response.Content.ReadAsStreamAsync()))?.OrderBy(j => j.BaseDateTime).ToArray();
			JmaRadarTimes = baseTimes;
			TimeSliderSize = JmaRadarTimes?.Length - 1 ?? 0;
			if (init)
				TimeSliderValue = baseTimes?.Length - 1 ?? TimeSliderSize;
			else
				UpdateTiles();
			IsLoading = false;
		}
		public async void UpdateTiles()
		{
			if (JmaRadarTimes == null || JmaRadarTimes.Length <= timeSliderValue)
				return;

			PullImageQueue.Clear();
			lock (WorkingUrls)
				WorkingUrls.Clear();

			var val = JmaRadarTimes[timeSliderValue];
			if (val is null)
				return;
			CurrentDateTime = val.ValidDateTime?.AddHours(9) ?? throw new Exception("ValidTime が取得できません");
			var oldLayer = ImageTileProviders?.FirstOrDefault();
			var baseDateTime = val.BaseDateTime ?? throw new Exception("BaseTime が取得できません");
			var validDateTime = val.ValidDateTime ?? throw new Exception("ValidTime が取得できません");
			ImageTileProviders = new Map.Layers.ImageTile.ImageTileProvider[]
			{
				new RadarImageTileProvider(this, baseDateTime, validDateTime)
			};
			if (oldLayer is RadarImageTileProvider ol)
				ol.Dispose();

			try
			{
				var url = $"https://www.jma.go.jp/bosai/jmatile/data/nowc/{baseDateTime:yyyyMMddHHmm00}/none/{validDateTime:yyyyMMddHHmm00}/surf/hrpns_nd/data.geojson?id=hrpns_nd";
				var geoJson = await JsonSerializer.DeserializeAsync<Models.GeoJson>(await InformationCacheService.TryGetOrFetchImageAsStreamAsync(url, async () =>
				{
					var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
					if (!response.IsSuccessStatusCode)
						throw new Exception("ステータスコード異常 status: " + response.StatusCode);
					return (await response.Content.ReadAsStreamAsync(), DateTime.Now.AddHours(3));
				}));

				if (geoJson != null)
				{
					var oldObject = RenderObjects?.FirstOrDefault();
					RenderObjects = new[]
					{
						new RadarNodataBorderRenderObject(geoJson)
					};
					if (oldObject is RadarNodataBorderRenderObject ro)
						ro.Dispose();
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("nodata範囲の取得に失敗: {ex}", ex);
			}
		}

		public override void Deactivated() { }

		public override void Dispose()
		{
			IsShutdown = true;
			SleepEvent.Set();
			if (ImageTileProviders?.FirstOrDefault() is RadarImageTileProvider l)
				l.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
