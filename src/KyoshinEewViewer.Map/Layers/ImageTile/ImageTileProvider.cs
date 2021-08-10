using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers.ImageTile
{
	public abstract class ImageTileProvider
	{
		public abstract int MinZoomLevel { get; }
		public abstract int MaxZoomLevel { get; }

		public event Action? ImageFetched;
		protected void OnImageFetched()
			=> ImageFetched?.Invoke();
		public abstract SKBitmap? GetOrStartFetchTileBitmap(int z, int x, int y);
	}
}
