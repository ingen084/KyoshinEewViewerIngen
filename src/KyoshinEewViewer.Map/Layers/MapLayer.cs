using Avalonia.Threading;
using SkiaSharp;
using System.Collections.Generic;

namespace KyoshinEewViewer.Map.Layers;

public abstract class MapLayer
{
	public double Zoom { get; set; }
	public PointD LeftTopLocation { get; set; }
	public PointD LeftTopPixel { get; set; }
	public RectD PixelBound { get; set; }
	public RectD ViewAreaRect { get; set; }

	private List<MapControl> AttachedControls { get; } = new();

	/// <summary>
	/// コントロールをアタッチする
	/// </summary>
	/// <param name="control">アタッチするコントロール</param>
	public void Attach(MapControl control) => AttachedControls.Add(control);

	/// <summary>
	/// コントロールをデタッチする
	/// </summary>
	/// <param name="control">デタッチするコントロール</param>
	public void Detach(MapControl control) => AttachedControls.Remove(control);

	/// <summary>
	/// アタッチされているコントロールに再描画を要求する
	/// </summary>
	protected void RefleshRequest()
		=> Dispatcher.UIThread.InvokeAsync(() =>
		{
			foreach (var control in AttachedControls.ToArray())
				control.InvalidateVisual();
		}).ConfigureAwait(false);

	/// <summary>
	/// 連続した更新が必要かどうか
	/// 描画時にこのフラグが有効なレイヤーが存在している場合、次フレームの描画が予約される
	/// </summary>
	public abstract bool NeedPersistentUpdate { get; }

	/// <summary>
	/// リソースのキャッシュを更新する
	/// レイヤー変更時必ず1度は呼ばれる
	/// </summary>
	/// <param name="targetControl">キャッシュ更新用のコントロール</param>
	public abstract void RefreshResourceCache(Avalonia.Controls.Control targetControl);

	/// <summary>
	/// 描画を行う
	/// </summary>
	/// <param name="canvas">描画対象</param>
	/// <param name="isAnimating">アニメーション(ナビゲーション)中かどうか</param>
	public abstract void Render(SKCanvas canvas, bool isAnimating);
}
