using KyoshinEewViewer.Map.Layers.ImageTile;
using KyoshinEewViewer.Services;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace KyoshinEewViewer.Series.Radar
{
	public class RadarImageTileProvider : ImageTileProvider
	{
		private RadarSeries Series { get; }
		private DateTime BaseTime { get; }
		private DateTime ValidTime { get; }
		public RadarImageTileProvider(RadarSeries series, DateTime baseTime, DateTime validTime)
		{
			Series = series;
			BaseTime = baseTime;
			ValidTime = validTime;
		}

		private ConcurrentDictionary<(int z, int x, int y), SKBitmap?> Cache { get; } = new();

		public override int MinZoomLevel { get; } = 4;

		public override int MaxZoomLevel { get; } = 10;

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
			void DW(string message)
			{
				if (sw.ElapsedMilliseconds != 0)
					Debug.WriteLine($"TryGetTileBitmap {message} {sw.Elapsed.TotalMilliseconds:0.00}ms");
			}
			var loc = (z, x, y);
			if (Cache.TryGetValue(loc, out bitmap))
			{
				DW("lc");
				return true;
			}
			var url = $"https://www.jma.go.jp/bosai/jmatile/data/nowc/{BaseTime:yyyyMMddHHmm00}/none/{ValidTime:yyyyMMddHHmm00}/surf/hrpns/{z}/{x}/{y}.png";
			if (InformationCacheService.Default.TryGetImage(url, out bitmap))
			{
				DW("fc");
				Cache[loc] = bitmap;
				return true;
			}
			if (doNotFetch)
			{
				DW("dnf");
				return false;
			}
			// 重複リクエスト防止はFetchImage側でやるので気軽に投げる
			Series.FetchImage(this, loc, url);
			DW("f");
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
}
