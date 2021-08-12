using KyoshinEewViewer.Map.Layers.ImageTile;
using KyoshinEewViewer.Services;
using SkiaSharp;
using System;
using System.Collections.Concurrent;

namespace KyoshinEewViewer.Series.Radar
{
	public class RadarImageTileProvider : ImageTileProvider, IDisposable
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

		public bool IsDisposing { get; private set; } = false;

		private ConcurrentDictionary<(int z, int x, int y), SKBitmap?> Cache { get; } = new();

		public override int MinZoomLevel { get; } = 4;

		public override int MaxZoomLevel { get; } = 10;

		public void OnImageUpdated((int z, int x, int y) loc, SKBitmap bitmap)
		{
			if (IsDisposing)
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
			var loc = (z, x, y);
			if (Cache.TryGetValue(loc, out bitmap))
				return true;
			var url = $"https://www.jma.go.jp/bosai/jmatile/data/nowc/{BaseTime:yyyyMMddHHmm00}/none/{ValidTime:yyyyMMddHHmm00}/surf/hrpns/{z}/{x}/{y}.png";
			if (InformationCacheService.Default.TryGetImage(url, out bitmap))
			{
				Cache[loc] = bitmap;
				return true;
			}
			if (doNotFetch)
				return false;
			// 重複リクエスト防止
			//Cache[loc] = null;
			Series.FetchImage(this, loc, url);
			// try
			// {
			// 	using var str = await RadarSeries.Client.GetStreamAsync($"https://www.jma.go.jp/bosai/jmatile/data/nowc/{BaseTime:yyyyMMddHHmm00}/none/{ValidTime:yyyyMMddHHmm00}/surf/hrpns/{z}/{x}/{y}.png");
			// 	if (IsDisposing)
			// 		return;
			// 	var bitmap = SKBitmap.Decode(str);
			// 	unsafe
			// 	{
			// 		var ptr = (uint*)bitmap.GetPixels().ToPointer();
			// 		var pixelCount = bitmap.Width * bitmap.Height;

			// 		// 透過画像に加工する
			// 		for (var i = 0; i < pixelCount; i++)
			// 			*ptr++ &= 0xAE_FF_FF_FF;
			// 	}
			// 	Cache[(z, x, y)] = bitmap;
			// 	if (bitmap != null)
			// 		OnImageFetched();
			// }
			// catch(Exception ex)
			// {
			// 	Debug.WriteLine(ex);
			// }
			return false;
		}

		public void Dispose()
		{
			IsDisposing = true;
			foreach (var b in Cache.Values)
				b?.Dispose();
			Cache.Clear();
			GC.SuppressFinalize(this);
		}
	}
}
