using Avalonia.Input;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Metrics;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace KyoshinEewViewer.Map.Layers;

public class MapLayerHost : IDisposable
{
	private readonly Lock _cacheLock = new();
	private LayerRenderParameter? _cachedParam;
	private readonly Dictionary<MapLayer, SKPicture> _layerCaches = [];
	private readonly List<SKPicture> _pendingDisposal = [];
	private readonly object _lifecycleLock = new();
	private volatile bool _isActive = true;
	private bool _isDisposed;
	private bool _resourceCacheRefreshPending;

	/// <summary>
	/// レイヤーの更新通知を受け付ける状態かどうか
	/// </summary>
	public bool IsActive => _isActive && !_isDisposed;

	/// <summary>
	/// 再描画が要求された
	/// </summary>
	public event Action? RefreshRequested;

	private void OnLayerRefreshRequested(MapLayer layer)
	{
		if (!IsActive)
			return;
		InvalidateLayerCache(layer);
		if (IsActive)
			RefreshRequested?.Invoke();
	}

	private WindowTheme? _windowTheme;
	/// <summary>
	/// ウィンドウテーマ
	/// </summary>
	public WindowTheme? WindowTheme
	{
		get => _windowTheme;
		set {
			var requestRefresh = false;
			lock (_lifecycleLock)
			{
				ObjectDisposedException.ThrowIf(_isDisposed, this);
				if (_windowTheme == value)
					return;
				_windowTheme = value;
				lock (_cacheLock)
					ClearAllCachesCore();
				if (IsActive && Layers is { } && _windowTheme is { })
				{
					foreach (var l in Layers)
						l.RefreshResourceCache(_windowTheme);
					_resourceCacheRefreshPending = false;
				}
				else
					_resourceCacheRefreshPending = Layers is not null && _windowTheme is not null;
				requestRefresh = IsActive;
			}
			if (requestRefresh && IsActive)
				RefreshRequested?.Invoke();
		}
	}

	private MapLayer[]? _layers;
	/// <summary>
	/// レイヤー
	/// </summary>
	public MapLayer[]? Layers
	{
		get => _layers;
		set {
			var requestRefresh = false;
			lock (_lifecycleLock)
			{
				ObjectDisposedException.ThrowIf(_isDisposed, this);
				if (IsActive && _layers is { })
					foreach (var l in _layers)
						l.RefreshRequested -= OnLayerRefreshRequested;
				lock (_cacheLock)
					ClearAllCachesCore();
				_layers = value;
				if (IsActive && _layers is { })
				{
					foreach (var l in _layers)
					{
						l.RefreshRequested += OnLayerRefreshRequested;
						if (WindowTheme is { })
							l.RefreshResourceCache(WindowTheme);
					}
					_resourceCacheRefreshPending = false;
				}
				else
					_resourceCacheRefreshPending = _layers is not null && WindowTheme is not null;
				requestRefresh = IsActive;
			}
			if (requestRefresh && IsActive)
				RefreshRequested?.Invoke();
		}
	}

	/// <summary>
	/// レイヤーの更新通知を再び購読する
	/// </summary>
	public void Activate()
	{
		lock (_lifecycleLock)
		{
			ObjectDisposedException.ThrowIf(_isDisposed, this);
			if (_isActive)
				return;

			_isActive = true;
			if (_layers is { })
			{
				foreach (var l in _layers)
					l.RefreshRequested += OnLayerRefreshRequested;
				if (_resourceCacheRefreshPending && WindowTheme is { })
				{
					foreach (var l in _layers)
						l.RefreshResourceCache(WindowTheme);
					_resourceCacheRefreshPending = false;
				}
			}
		}
		if (IsActive)
			RefreshRequested?.Invoke();
	}

	/// <summary>
	/// レイヤーの更新通知を解除し、描画キャッシュを解放する
	/// </summary>
	public void Deactivate()
	{
		lock (_lifecycleLock)
		{
			if (!_isActive)
				return;

			_isActive = false;
			if (_layers is { })
				foreach (var l in _layers)
					l.RefreshRequested -= OnLayerRefreshRequested;
			lock (_cacheLock)
			{
				ClearAllCachesCore();
				FlushPendingDisposals();
			}
		}
	}

	/// <summary>
	/// レイヤーの描画を行う
	/// </summary>
	/// <param name="canvas">描画対象のキャンバス</param>
	/// <param name="param">描画パラメータ</param>
	/// <param name="isAnimating">アニメーション中かどうか</param>
	/// <returns>次フレームの描画を即時行った方が良いか</returns>
	public bool Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (!IsActive || Layers is null)
			return false;

		lock (_cacheLock)
		{
			if (!IsActive)
				return false;
			// レンダリング開始時に遅延解放待ちのリソースを解放
			FlushPendingDisposals();

			if (_cachedParam is null || !_cachedParam.Value.Equals(param))
			{
				ClearAllCachesCore();
				_cachedParam = param;
			}

			var needPersistentUpdate = false;
			foreach (var l in Layers)
			{
				RenderLayerCore(canvas, l, param, isAnimating);
				if (l.NeedPersistentUpdate)
				{
					needPersistentUpdate = true;
					InvalidateLayerCacheCore(l);
				}
			}
			return needPersistentUpdate;
		}
	}

	/// <remarks>呼び出し元で _cacheLock を取得していること</remarks>
	private void RenderLayerCore(SKCanvas canvas, MapLayer layer, LayerRenderParameter param, bool isAnimating)
	{
		if (_layerCaches.TryGetValue(layer, out var cached))
		{
			canvas.DrawPicture(cached);
			return;
		}

		using var recorder = new SKPictureRecorder();
		var bounds = new SKRect(
			0,
			0,
			(float)param.PixelBound.Width,
			(float)param.PixelBound.Height);
		var recCanvas = recorder.BeginRecording(bounds);
		layer.Render(recCanvas, param, isAnimating);
		var picture = recorder.EndRecording();

		_layerCaches[layer] = picture;
		canvas.DrawPicture(picture);
	}

	/// <summary>
	/// レイヤーの描画を行い、パフォーマンスメトリクスを記録する
	/// </summary>
	/// <param name="canvas">描画対象のキャンバス</param>
	/// <param name="param">描画パラメータ</param>
	/// <param name="isAnimating">アニメーション中かどうか</param>
	/// <param name="layerMetrics">各レイヤーのメトリクス（出力）</param>
	/// <returns>次フレームの描画を即時行った方が良いか</returns>
	public bool RenderWithMetrics(SKCanvas canvas, LayerRenderParameter param, bool isAnimating, out List<LayerRenderMetrics> layerMetrics)
	{
		layerMetrics = [];
		if (!IsActive || Layers is null)
			return false;

		lock (_cacheLock)
		{
			if (!IsActive)
				return false;
			// レンダリング開始時に遅延解放待ちのリソースを解放
			FlushPendingDisposals();

			if (_cachedParam is null || !_cachedParam.Value.Equals(param))
			{
				ClearAllCachesCore();
				_cachedParam = param;
			}

			var needPersistentUpdate = false;
			var timestamp = DateTime.Now;

			foreach (var l in Layers)
			{
				var sw = Stopwatch.StartNew();
				RenderLayerCore(canvas, l, param, isAnimating);
				sw.Stop();

				var metrics = new LayerRenderMetrics
				{
					LayerName = l.GetType().Name,
					RenderTime = sw.Elapsed,
					RenderInfo = l.GetRenderInfo(),
					Timestamp = timestamp
				};

				l.LastRenderMetrics = metrics;
				layerMetrics.Add(metrics);

				if (l.NeedPersistentUpdate)
				{
					needPersistentUpdate = true;
					InvalidateLayerCacheCore(l);
				}
			}

			return needPersistentUpdate;
		}
	}

	/// <summary>
	/// マウスクリックイベントをレイヤーに伝播する
	/// </summary>
	/// <param name="location">クリックした位置（緯度経度）</param>
	/// <param name="screenPosition">クリックした画面座標</param>
	/// <param name="button">クリックしたボタン</param>
	/// <param name="param">レンダリングパラメータ</param>
	/// <returns>いずれかのレイヤーでイベントが処理されたかどうか</returns>
	public bool OnMouseClick(Location location, PointD screenPosition, MouseButton button, LayerRenderParameter param)
	{
		if (!IsActive)
			return false;
		// バックグラウンドスレッドからの Layers 差し替えと競合しないよう、ローカルにキャプチャしてから参照する
		var layers = Layers;
		if (layers is null)
			return false;

		// 逆順でチェック（上位レイヤーを優先）
		for (int i = layers.Length - 1; i >= 0; i--)
		{
			if (layers[i].OnMouseClick(location, screenPosition, button, param))
				return true;
		}
		return false;
	}

	/// <summary>
	/// マウス(ポインタ)移動イベントをレイヤーに伝播する
	/// </summary>
	/// <param name="location">移動先の位置（緯度経度）</param>
	/// <param name="screenPosition">移動先の画面座標</param>
	/// <param name="param">レンダリングパラメータ</param>
	/// <returns>いずれかのレイヤーでイベントが処理されたかどうか</returns>
	public bool OnPointerMoved(Location location, PointD screenPosition, LayerRenderParameter param)
	{
		if (!IsActive)
			return false;
		// バックグラウンドスレッドからの Layers 差し替えと競合しないよう、ローカルにキャプチャしてから参照する
		var layers = Layers;
		if (layers is null)
			return false;

		// 逆順でチェック（上位レイヤーを優先）
		for (int i = layers.Length - 1; i >= 0; i--)
		{
			if (layers[i].OnPointerMoved(location, screenPosition, param))
				return true;
		}
		return false;
	}

	/// <summary>
	/// ポインタが描画領域外へ出たイベントを全レイヤーに伝播する
	/// </summary>
	public void OnPointerExited()
	{
		if (!IsActive)
			return;
		var layers = Layers;
		if (layers is null)
			return;

		foreach (var l in layers)
			l.OnPointerExited();
	}

	private void InvalidateLayerCache(MapLayer layer)
	{
		lock (_cacheLock)
			InvalidateLayerCacheCore(layer);
	}

	/// <remarks>呼び出し元で _cacheLock を取得していること</remarks>
	private void InvalidateLayerCacheCore(MapLayer layer)
	{
		if (_layerCaches.Remove(layer, out var pic))
			_pendingDisposal.Add(pic);
	}

	/// <remarks>呼び出し元で _cacheLock を取得していること</remarks>
	private void ClearAllCachesCore()
	{
		_pendingDisposal.AddRange(_layerCaches.Values);
		_layerCaches.Clear();
		_cachedParam = null;
	}

	/// <summary>
	/// 遅延解放待ちのSKPictureを解放する
	/// </summary>
	/// <remarks>呼び出し元で _cacheLock を取得していること</remarks>
	private void FlushPendingDisposals()
	{
		if (_pendingDisposal.Count == 0)
			return;
		foreach (var pic in _pendingDisposal)
			pic.Dispose();
		_pendingDisposal.Clear();
	}

	public void Dispose()
	{
		lock (_lifecycleLock)
		{
			if (_isDisposed)
				return;
			Deactivate();
			_isDisposed = true;
			_layers = null;
			_windowTheme = null;
			RefreshRequested = null;
		}
		GC.SuppressFinalize(this);
	}
}
