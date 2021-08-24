using Avalonia.Controls;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace KyoshinEewViewer.Series.Radar
{
	public class RadarSeries : SeriesBase
	{
		public static HttpClient Client { get; } = new();

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
							Debug.WriteLine($"{DateTime.Now:ss.FFF} thread{threadNumber} pulling {data.url}");
							try
							{
								using var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, data.url));
								if (!response.IsSuccessStatusCode)
									continue;
								data.sender.OnImageUpdated(data.loc, InformationCacheService.Default.SetImageCache(data.url, DateTime.Now.AddHours(1), await response.Content.ReadAsStreamAsync()/*, bitmap =>
								{
									unsafe
									{
										var ptr = (uint*)bitmap.GetPixels().ToPointer();
										var pixelCount = bitmap.Width * bitmap.Height;
										// 透過画像に加工する
										for (var i = 0; i < pixelCount; i++)
											*ptr++ &= 0xDD_FF_FF_FF;
									}
									return bitmap;
								}*/));
								Debug.WriteLine($"{DateTime.Now:ss.FFF} thread{threadNumber} pulled {data.url}");
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine(ex);
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
		}
		public async void Reload(bool init = false)
		{
			IsLoading = true;
			var baseTimes = (await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await Client.GetStreamAsync("https://www.jma.go.jp/bosai/jmatile/data/nowc/targetTimes_N1.json")))?.OrderBy(j => j.BaseDateTime).ToArray();
			JmaRadarTimes = baseTimes;//?.Concat((await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await Client.GetStreamAsync("https://www.jma.go.jp/bosai/jmatile/data/nowc/targetTimes_N2.json")))?.OrderBy(j => j.ValidDateTime)).ToArray();
			TimeSliderSize = JmaRadarTimes?.Length - 1 ?? 0;
			if (init)
				TimeSliderValue = baseTimes?.Length - 1 ?? TimeSliderSize;
			else
				UpdateTiles();
			IsLoading = false;
		}
		public void UpdateTiles()
		{
			InformationCacheService.Default.CleanupImageCache();
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
			ImageTileProviders = new Map.Layers.ImageTile.ImageTileProvider[]
			{
				new RadarImageTileProvider(
					this,
					val.BaseDateTime ?? throw new Exception("BaseTime が取得できません"),
					val.ValidDateTime ?? throw new Exception("ValidTime が取得できません"))
			};
			if (oldLayer is RadarImageTileProvider ol)
				ol.Dispose();
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

	public class JmaRadarTime
	{
		[JsonPropertyName("basetime")]
		public string? BaseTime { get; set; }
		[JsonIgnore]
		public DateTime? BaseDateTime => DateTime.TryParseExact(BaseTime, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var time)
					? time
					: null;
		[JsonPropertyName("validtime")]
		public string? ValidTime { get; set; }
		[JsonIgnore]
		public DateTime? ValidDateTime => DateTime.TryParseExact(ValidTime, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var time)
					? time
					: null;
		[JsonPropertyName("elements")]
		public string[]? Elements { get; set; }
	}

}
