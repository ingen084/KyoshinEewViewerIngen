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

	/// <summary>
	/// 再描画が要求された
	/// </summary>
	public event Action? RefreshRequested;

	private void OnLayerRefreshRequested(MapLayer layer)
	{
		InvalidateLayerCache(layer);
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
			if (_windowTheme == value)
				return;
			_windowTheme = value;
			lock (_cacheLock)
				ClearAllCachesCore();
			if (Layers is { } && _windowTheme is { })
				foreach (var l in Layers)
					l.RefreshResourceCache(_windowTheme);
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
			if (_layers is { })
				foreach (var l in _layers)
					l.RefreshRequested -= OnLayerRefreshRequested;
			lock (_cacheLock)
				ClearAllCachesCore();
			_layers = value;
			if (_layers is { })
				foreach (var l in _layers)
				{
					l.RefreshRequested += OnLayerRefreshRequested;
					if (WindowTheme is { })
						l.RefreshResourceCache(WindowTheme);
				}
			RefreshRequested?.Invoke();
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
		if (Layers is null)
			return false;

		lock (_cacheLock)
		{
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
		if (Layers is null)
			return false;

		lock (_cacheLock)
		{
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
		if (Layers is null)
			return false;

		// 逆順でチェック（上位レイヤーを優先）
		for (int i = Layers.Length - 1; i >= 0; i--)
		{
			if (Layers[i].OnMouseClick(location, screenPosition, button, param))
				return true;
		}
		return false;
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
		lock (_cacheLock)
		{
			ClearAllCachesCore();
			// Dispose時は遅延解放待ちのリソースも即座に解放
			FlushPendingDisposals();
		}
		if (_layers is { })
			foreach (var l in _layers)
				l.RefreshRequested -= OnLayerRefreshRequested;
		GC.SuppressFinalize(this);
	}
}
