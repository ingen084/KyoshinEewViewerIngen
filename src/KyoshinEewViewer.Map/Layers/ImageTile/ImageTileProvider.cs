using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers.ImageTile
{
	public abstract class ImageTileProvider : IDisposable
	{
		public abstract int MinZoomLevel { get; }
		public abstract int MaxZoomLevel { get; }

		public event Action? ImageFetched;
		protected void OnImageFetched()
			=> ImageFetched?.Invoke();
		public abstract bool TryGetTileBitmap(int z, int x, int y, bool doNotFetch, out SKBitmap? bitmap);
		public bool IsDisposed { get; protected set; }
		public abstract void Dispose();
	}
}
