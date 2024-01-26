using KyoshinEewViewer.Core.Models;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers;

public abstract class MapLayer
{
	/// <summary>
	/// 再描画が要求された
	/// </summary>
	public event Action? RefreshRequested;

	/// <summary>
	/// アタッチされているコントロールに再描画を要求する
	/// </summary>
	protected void RefreshRequest()
		=> RefreshRequested?.Invoke();

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
	public abstract void RefreshResourceCache(WindowTheme targetControl);

	/// <summary>
	/// 描画を行う
	/// </summary>
	/// <param name="canvas">描画対象</param>
	/// <param name="param">描画する範囲の情報</param>
	/// <param name="isAnimating">アニメーション(ナビゲーション)中かどうか</param>
	public abstract void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating);
}
