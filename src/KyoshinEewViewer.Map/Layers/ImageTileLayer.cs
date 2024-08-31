using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map.Layers.ImageTile;
using KyoshinEewViewer.Map.Projections;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers;

public class ImageTileLayer : MapLayer
{
	private static readonly SKPaint PlaceHolderPaint = new()
	{
		Style = SKPaintStyle.StrokeAndFill,
		Color = new SKColor(255, 0, 0, 50),
		PathEffect = SKPathEffect.Create2DLine(0, SKMatrix.CreateScale(8, 8).PreConcat(SKMatrix.CreateRotationDegrees(-30, 0, 0)))
	};
#if DEBUG
	private static readonly SKPaint DebugPen = new()
	{
		Style = SKPaintStyle.Fill,
		Color = SKColors.Red.WithAlpha(127),
	};
	private static readonly SKPaint DebugBorderPen = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.White.WithAlpha(100),
		StrokeWidth = 2,
	};
#endif
	private static readonly SKPaint ImageBlender = new()
	{
		// ImageFilter = SKImageFilter.CreateBlendMode(SKBlendMode.SrcIn, SKImageFilter.CreateColorFilter(SKColorFilter.CreateBlendMode(SKColors.White.WithAlpha(200), SKBlendMode.SrcIn))),
	};

	public ImageTileProvider Provider { get; }
	public MercatorProjection MercatorProjection { get; } = new();

	public ImageTileLayer(ImageTileProvider provider)
	{
		Provider = provider;
		Provider.ImageFetched += () => RefreshRequest();
	}

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme windowTheme) { }

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Provider.IsDisposed)
			return;
		lock (Provider)
		{
			canvas.Save();
			try
			{
				// 使用するキャッシュのズーム
				var baseZoom = Provider.GetTileZoomLevel(param.Zoom);
				// 実際のズームに合わせるためのスケール
				var scale = Math.Pow(2, param.Zoom - baseZoom);
				canvas.Scale((float)scale);
				// 画面座標への変換
				var leftTop = param.LeftTopLocation.CastLocation().ToPixel(baseZoom);
				canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

				// メルカトル図法でのピクセル座標を求める
				var mercatorPixelRect = new RectD(
					param.ViewAreaRect.TopLeft.CastLocation().ToPixel(baseZoom, MercatorProjection),
					param.ViewAreaRect.BottomRight.CastLocation().ToPixel(baseZoom, MercatorProjection));

				// タイルのオフセット
				var xTileOffset = (int)(mercatorPixelRect.Left / MercatorProjection.TileSize);
				var yTileOffset = (int)(mercatorPixelRect.Top / MercatorProjection.TileSize);

				// 表示するタイルの数
				var xTileCount = (int)(mercatorPixelRect.Width / MercatorProjection.TileSize) + 2;
				var yTileCount = (int)(mercatorPixelRect.Height / MercatorProjection.TileSize) + 2;

				// タイルを描画し始める左上のピクセル座標
				var tileOrigin = new PointD(mercatorPixelRect.Left - (mercatorPixelRect.Left % MercatorProjection.TileSize), mercatorPixelRect.Top - (mercatorPixelRect.Top % MercatorProjection.TileSize));

				for (var y = 0; y < yTileCount; y++)
				{
					if (yTileOffset + y < 0)
						continue;
					var cy = (float)new PointD(0, tileOrigin.Y + y * MercatorProjection.TileSize).ToLocation(baseZoom, MercatorProjection).ToPixel(baseZoom).Y;
					var ch = (float)Math.Abs(cy - new PointD(0, tileOrigin.Y + (y + 1) * MercatorProjection.TileSize).ToLocation(baseZoom, MercatorProjection).ToPixel(baseZoom).Y);
					for (var x = 0; x < xTileCount; x++)
					{
						if (xTileOffset + x < 0)
							continue;

						var cx = (float)(tileOrigin.X + x * MercatorProjection.TileSize);
						var tx = xTileOffset + x;
						var ty = yTileOffset + y;
						if (Provider.TryGetTileBitmap(baseZoom, tx, ty, isAnimating, out var image) && image is { })
						{
							canvas.DrawBitmap(image, new SKRect(cx, cy, cx + MercatorProjection.TileSize, cy + ch), ImageBlender);

#if DEBUG
							canvas.DrawText($"Z{baseZoom} {{{xTileOffset + x}, {yTileOffset + y}}}", cx, cy, DebugBorderPen);
							canvas.DrawText($"Z{baseZoom} {{{xTileOffset + x}, {yTileOffset + y}}}", cx, cy, DebugPen);
#endif
						}
						// -1 ズーム倍率へのフォールバック
						//else if (Provider.TryGetTileBitmap(baseZoom - 1, tx / 2, ty / 2, true, out image) && image is { })
						//{
						//	var halfTile = MercatorProjection.TileSize / 2;
						//	var xf = (tx % 2) * halfTile;
						//	var yf = (ty % 2) * halfTile;
						//	canvas.DrawBitmap(image, new SKRect(xf, yf, xf + halfTile, yf + halfTile)/*, new SKRect(cx, cy, cx + MercatorProjection.TileSize, cy + ch)*/, ImageBlender);
						//}
						else
						{
#if DEBUG
							canvas.DrawLine(new SKPoint(cx, cy), new SKPoint(cx, cy + ch - 2), DebugPen);
							canvas.DrawLine(new SKPoint(cx, cy), new SKPoint(cx + MercatorProjection.TileSize - 2, cy), DebugPen);
#endif
							canvas.DrawRect(cx, cy, MercatorProjection.TileSize, ch, PlaceHolderPaint);
						}
					}
				}
			}
			finally
			{
				canvas.Restore();
			}
		}
	}
}
