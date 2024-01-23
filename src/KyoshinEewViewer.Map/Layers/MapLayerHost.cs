using KyoshinEewViewer.Core.Models;
using SkiaSharp;
using System;

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
}
