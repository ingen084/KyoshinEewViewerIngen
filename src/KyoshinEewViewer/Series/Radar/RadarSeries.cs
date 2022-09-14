using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Radar.Models;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace KyoshinEewViewer.Series.Radar;

public class RadarSeries : SeriesBase
{
	public static HttpClient Client { get; } = new(new HttpClientHandler()
	{
		AutomaticDecompression = DecompressionMethods.All
	});
	private ILogger Logger { get; }

	private DateTime _currentDateTime = DateTime.Now;
	public DateTime CurrentDateTime
	{
		get => _currentDateTime;
		set => this.RaiseAndSetIfChanged(ref _currentDateTime, value);
	}
	private bool _isLoading = true;
	public bool IsLoading
	{
		get => _isLoading;
		set => this.RaiseAndSetIfChanged(ref _isLoading, value);
	}

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
	private int _timeSliderSize = 1;
	public int TimeSliderSize
	{
		get => _timeSliderSize;
		set => this.RaiseAndSetIfChanged(ref _timeSliderSize, value);
	}

	private JmaRadarTime[]? _jmaRadarTimes;
	public JmaRadarTime[]? JmaRadarTimes
	{
		get => _jmaRadarTimes;
		set => this.RaiseAndSetIfChanged(ref _jmaRadarTimes, value);
	}

	// 気象庁にリクエストを投げるスレッド数
	// ブラウザは基本6だがXMLの取得などもあるので5
	private const int PullImageThreadCount = 5;
	public RadarNodataBorderLayer BorderLayer { get; set; }

	private Thread[] PullImageThreads { get; }
	private ConcurrentQueue<(RadarImageTileProvider sender, (int z, int x, int y) loc, string url)> PullImageQueue { get; } = new();
	private List<string> WorkingUrls { get; } = new();
	private bool IsShutdown { get; set; }
	private ManualResetEventSlim SleepEvent { get; } = new(false);
	public RadarSeries() : base("雨雲(β)", new FontIcon { Glyph = "\xf740", FontFamily = new("IconFont") })
	{
		Logger = LoggingService.CreateLogger(this);
		MapPadding = new Avalonia.Thickness(0, 50, 0, 0);
		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Assembly.GetExecutingAssembly().GetName().Version};twitter@ingen084");

		BorderLayer = new();
		OverlayLayers = new[] { BorderLayer };

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

		try
		{
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
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "レーダー更新中にエラー");
		}
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
		var oldLayer = BaseLayers?.FirstOrDefault() as ImageTileLayer;
		var baseDateTime = val.BaseDateTime ?? throw new Exception("BaseTime が取得できません");
		var validDateTime = val.ValidDateTime ?? throw new Exception("ValidTime が取得できません");
		BaseLayers = new[] { new ImageTileLayer(new RadarImageTileProvider(this, baseDateTime, validDateTime)) };
		if (oldLayer is not null)
			oldLayer.Provider.Dispose();

		try
		{
			var url = $"https://www.jma.go.jp/bosai/jmatile/data/nowc/{baseDateTime:yyyyMMddHHmm00}/none/{validDateTime:yyyyMMddHHmm00}/surf/hrpns_nd/data.geojson?id=hrpns_nd";
			var geoJson = await JsonSerializer.DeserializeAsync<GeoJson>(await InformationCacheService.TryGetOrFetchImageAsStreamAsync(url, async () =>
			{
				var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
				if (!response.IsSuccessStatusCode)
					throw new Exception("ステータスコード異常 status: " + response.StatusCode);
				return (await response.Content.ReadAsStreamAsync(), DateTime.Now.AddHours(3));
			}));

			if (geoJson != null)
				BorderLayer.UpdatePoints(geoJson);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "nodata範囲の取得に失敗");
		}
	}

	public override void Deactivated() { }

	public override void Dispose()
	{
		IsShutdown = true;
		SleepEvent.Set();
		if (BaseLayers?.FirstOrDefault() is ImageTileLayer l)
			l.Provider.Dispose();
		GC.SuppressFinalize(this);
	}
}
