using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using SkiaSharp;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Ml.Layers;

/// <summary>
/// 機械学習用グリッドレイヤー
/// </summary>
public class MlGridLayer : MapLayer
{
	private SKPaint? _gridPaint;

	/// <summary>
	/// グリッドセルのリスト
	/// </summary>
	public IReadOnlyList<GridCell> GridCells { get; private set; } = [];

	public override bool NeedPersistentUpdate => false;

	/// <summary>
	/// グリッドセルを更新する
	/// </summary>
	/// <param name="cells">新しいグリッドセルのリスト</param>
	public void UpdateGridCells(IReadOnlyList<GridCell> cells)
	{
		GridCells = cells;
		RefreshRequest();
	}

	public override void RefreshResourceCache(WindowTheme theme)
	{
		_gridPaint = new SKPaint
		{
			Color = new SKColor(0, 255, 0, 128),
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1,
			IsAntialias = true
		};
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (GridCells.Count == 0 || _gridPaint == null)
			return;

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			foreach (var cell in GridCells)
			{
				// グリッドセルの四隅の座標を計算
				var topLeft = new Location((float)cell.MaxLatitude, (float)cell.MinLongitude).ToPixel(param.Zoom);
				var bottomRight = new Location((float)cell.MinLatitude, (float)cell.MaxLongitude).ToPixel(param.Zoom);

				// 画面外の場合はスキップ
				var cellRect = new RectD(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

				if (!param.PixelBound.IntersectsWith(cellRect))
					continue;

				// グリッドセルを描画
				canvas.DrawRect(cellRect.AsSkRect(), _gridPaint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}

/// <summary>
/// グリッドセル
/// </summary>
public record GridCell(
	double MinLatitude,
	double MaxLatitude,
	double MinLongitude,
	double MaxLongitude,
	int ObservationPointCount);
