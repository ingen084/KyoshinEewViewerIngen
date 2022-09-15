using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series;

public abstract class SeriesBase : ReactiveObject, IDisposable
{
	public event Action<MapNavigationRequested?>? MapNavigationRequested;
	protected void OnMapNavigationRequested(MapNavigationRequested? arg)
	{
		if (_focusBound == arg?.Bound)
			return;
		if (_focusBound == null || arg?.Bound == null)
		{
			FocusBound = arg?.Bound;
			return;
		}
		_focusBound = arg?.Bound;
		MapNavigationRequested?.Invoke(arg);
	}

	protected SeriesBase(string name, FAIconElement? icon = null)
	{
		Name = name;
		Icon = icon ?? new SymbolIcon() { Symbol = Symbol.Alert };
	}

	private bool _isActivated;
	public bool IsActivated
	{
		get => _isActivated;
		set => this.RaiseAndSetIfChanged(ref _isActivated, value);
	}

	public string Name { get; }

	public FAIconElement Icon { get; }

	private MapLayer[]? _baseLayers;
	/// <summary>
	/// ベースレイヤー
	/// 境界線よりも優先度が低い
	/// </summary>
	public MapLayer[]? BaseLayers
	{
		get => _baseLayers;
		protected set => this.RaiseAndSetIfChanged(ref _baseLayers, value);
	}

	private MapLayer[]? _overlayLayers;
	/// <summary>
	/// オーバーレイレイヤー
	/// 境界線よりも優先度が高い
	/// </summary>
	public MapLayer[]? OverlayLayers
	{
		get => _overlayLayers;
		protected set => this.RaiseAndSetIfChanged(ref _overlayLayers, value);
	}

	private Dictionary<LandLayerType, Dictionary<int, SKColor>>? _customColorMap;
	/// <summary>
	/// 地図に着色する内容のマップ
	/// </summary>
	public Dictionary<LandLayerType, Dictionary<int, SKColor>>? CustomColorMap
	{
		get => _customColorMap;
		protected set => this.RaiseAndSetIfChanged(ref _customColorMap, value);
	}

	private Rect? _focusBound;
	/// <summary>
	/// 現在この Series が表示させたい地図上での範囲
	/// </summary>
	public Rect? FocusBound
	{
		get => _focusBound;
		protected set => this.RaiseAndSetIfChanged(ref _focusBound, value);
	}

	private SeriesEvent? _event;
	/// <summary>
	/// この Series で発生しているイベント
	/// </summary>
	public SeriesEvent? Event
	{
		get => _event;
		protected set => this.RaiseAndSetIfChanged(ref _event, value);
	}

	/// <summary>
	/// タブ内部に表示させるコントロール
	/// </summary>
	public abstract Control DisplayControl { get; }

	private Thickness _mapPadding;
	public Thickness MapPadding
	{
		get => _mapPadding;
		protected set => this.RaiseAndSetIfChanged(ref _mapPadding, value);
	}

	public abstract void Activating();
	public abstract void Deactivated();

	public virtual void Dispose()
		=> GC.SuppressFinalize(this);
}

/// <summary>
/// Series で発生しているイベント
/// </summary>
/// <param name="Priority">優先度 0 が基準</param>
public record class SeriesEvent(int Priority)
{
	public SeriesBase? BeforeSeries { get; set; }
}
