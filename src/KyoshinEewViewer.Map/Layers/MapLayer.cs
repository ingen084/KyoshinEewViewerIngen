using Avalonia.Input;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Metrics;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Map.Layers;

public abstract class MapLayer
{
	/// <summary>
	/// 再描画が要求された
	/// 引数1はキャッシュを無効化すべきレイヤー（this）、
	/// 引数2はホストの直近の描画パラメータに対して再描画が必要かを判定する述語（null の場合は無条件）
	/// </summary>
	public event Action<MapLayer, Predicate<LayerRenderParameter>?>? RefreshRequested;

	/// <summary>
	/// アタッチされているコントロールに再描画を要求し、このレイヤーのキャッシュを無効化する
	/// </summary>
	protected void RefreshRequest()
		=> RefreshRequested?.Invoke(this, null);

	/// <summary>
	/// 描画内容に影響があるホストにのみ再描画を要求し、そのホストのこのレイヤーのキャッシュを無効化する
	/// </summary>
	/// <param name="isAffected">ホストの直近の描画パラメータに対して再描画が必要かを判定する述語</param>
	protected void RefreshRequest(Predicate<LayerRenderParameter> isAffected)
		=> RefreshRequested?.Invoke(this, isAffected);

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

	/// <summary>
	/// マウスクリックイベントを処理する
	/// </summary>
	/// <param name="location">クリックした位置（緯度経度）</param>
	/// <param name="screenPosition">クリックした画面座標</param>
	/// <param name="button">クリックしたボタン</param>
	/// <param name="param">レンダリングパラメータ</param>
	/// <returns>イベントが処理されたかどうか</returns>
	public virtual bool OnMouseClick(Location location, PointD screenPosition, MouseButton button, LayerRenderParameter param)
		=> false;

	/// <summary>
	/// マウス(ポインタ)移動イベントを処理する
	/// </summary>
	/// <param name="location">移動先の位置（緯度経度）</param>
	/// <param name="screenPosition">移動先の画面座標</param>
	/// <param name="param">レンダリングパラメータ</param>
	/// <returns>イベントが処理されたかどうか</returns>
	public virtual bool OnPointerMoved(Location location, PointD screenPosition, LayerRenderParameter param)
		=> false;

	/// <summary>
	/// ポインタが描画領域外に出た際の処理を行う
	/// </summary>
	public virtual void OnPointerExited() { }

	/// <summary>
	/// 最後の描画メトリクス
	/// </summary>
	public LayerRenderMetrics? LastRenderMetrics { get; internal set; }

	/// <summary>
	/// レイヤーの描画内容情報を取得する
	/// パフォーマンスメトリクスに含める情報を返す
	/// </summary>
	/// <returns>描画内容情報のKeyValueペア（例: { {"EEW件数", 3} }）、情報がない場合はnull</returns>
	public virtual IReadOnlyDictionary<string, object?>? GetRenderInfo()
		=> null;
}
