using Avalonia.Controls;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Map.Layers.ImageTile;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CustomRenderItemTest.Views;
public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();

		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => Map.RefreshResourceCache(x!.Theme));

		Map.Zoom = 6;
		Map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		Task.Run(() =>
		{
			var mapData = MapData.LoadDefaultMap();
			var landLayer = new LandLayer { Map = mapData };
			var landBorderLayer = new LandBorderLayer { Map = mapData };
			Map.Layers = [
				landLayer,
				landBorderLayer,
				// new ImageTileLayer(new GsiImageTileProvider(new ImageTilePuller(new HttpClient()))),
				new GridLayer(),
			];
		});
	}

	public class GsiImageTileProvider(ImageTilePuller puller) : ImageTileProvider
	{
		private ImageTilePuller Puller { get; } = puller;

		private ConcurrentDictionary<(int z, int x, int y), SKBitmap?> Cache { get; } = new();

		public int MinZoomLevel { get; } = 4;
		public int MaxZoomLevel { get; } = 18;

		public override int GetTileZoomLevel(double zoom)
			=> Math.Clamp((int)zoom, MinZoomLevel, MaxZoomLevel);

		public void OnImageUpdated((int z, int x, int y) loc, SKBitmap bitmap)
		{
			if (IsDisposed)
			{
				bitmap.Dispose();
				return;
			}
			Cache[loc] = bitmap;
			if (bitmap != null)
				OnImageFetched();
		}

		public override bool TryGetTileBitmap(int z, int x, int y, bool doNotFetch, out SKBitmap? bitmap)
		{
			var sw = Stopwatch.StartNew();
			void Dw(string message)
			{
				if (sw.ElapsedMilliseconds != 0)
					Debug.WriteLine($"TryGetTileBitmap {message} {sw.Elapsed.TotalMilliseconds:0.00}ms");
			}
			var loc = (z, x, y);
			if (Cache.TryGetValue(loc, out bitmap))
			{
				Dw("in-memory cache");
				return true;
			}
			var url = $"https://cyberjapandata.gsi.go.jp/xyz/std/{z}/{x}/{y}.png";
			if (doNotFetch)
				return false;

			// 重複リクエスト防止はFetchImage側でやるので気軽に投げる
			Puller.FetchImage(this, loc, url);
			Dw("fetch");
			return false;
		}

		public override void Dispose()
		{
			IsDisposed = true;
			lock (this)
			{
				foreach (var b in Cache.Values)
					b?.Dispose();
				Cache.Clear();
			}
			GC.SuppressFinalize(this);
		}
	}


	public class ImageTilePuller
	{
		// 気象庁にリクエストを投げるスレッド数
		// ブラウザは基本6だがXMLの取得などもあるので5
		private const int PullImageThreadCount = 5;

		private HttpClient Client { get; }

		private Thread[]? PullImageThreads { get; set; }
		private ConcurrentQueue<(GsiImageTileProvider sender, (int z, int x, int y) loc, string url)> PullImageQueue { get; } = new();
		private List<string> WorkingUrls { get; } = [];
		private bool IsShutdown { get; set; }
		private ManualResetEventSlim SleepEvent { get; } = new(false);

		public ImageTilePuller(HttpClient client)
		{
			Client = client;

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
								using var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, data.url));
								if (!response.IsSuccessStatusCode)
									throw new Exception($"タイル画像の取得に失敗しました({response.StatusCode}) " + data.url);
								var bitmap = SKBitmap.Decode(await response.Content.ReadAsStreamAsync());
								data.sender.OnImageUpdated(
									data.loc,
									bitmap
								);
								if (sw.ElapsedMilliseconds > 0)
									Debug.WriteLine($"{DateTime.Now:ss.FFF} pulled {sw.Elapsed.TotalMilliseconds:0.00}ms thread{threadNumber} {data.url}");
							}
							catch (Exception ex)
							{
								Debug.WriteLine("タイル画像の取得に失敗 " + ex);
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

		public void FetchImage(GsiImageTileProvider sender, (int z, int x, int y) loc, string url)
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
}
