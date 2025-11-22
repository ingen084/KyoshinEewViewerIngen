using Avalonia.Input;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Metrics;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KyoshinEewViewer.Map.Layers;

public class MapLayerHost
{
	/// <summary>
	/// 再描画が要求された
	/// </summary>
	public event Action? RefreshRequested;

	private void RefreshRequest()
		=> RefreshRequested?.Invoke();

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
			if (Layers is { } && _windowTheme is { })
				foreach (var l in Layers)
					l.RefreshResourceCache(_windowTheme);
			RefreshRequest();
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
					l.RefreshRequested -= RefreshRequest;
			_layers = value;
			if (_layers is { })
				foreach (var l in _layers)
				{
					l.RefreshRequested += RefreshRequest;
					if (WindowTheme is { })
						l.RefreshResourceCache(WindowTheme);
				}
			RefreshRequest();
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
		var needPersistentUpdate = false;
		foreach (var l in Layers)
		{
			l.Render(canvas, param, isAnimating);
			if (l.NeedPersistentUpdate)
				needPersistentUpdate = true;
		}
		return needPersistentUpdate;
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

		var needPersistentUpdate = false;
		var timestamp = DateTime.Now;

		foreach (var l in Layers)
		{
			var sw = Stopwatch.StartNew();
			l.Render(canvas, param, isAnimating);
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
				needPersistentUpdate = true;
		}

		return needPersistentUpdate;
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
}
