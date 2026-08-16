using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series;
using R3;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.ViewModels;

public class SeriesWindowViewModel : ViewModelBase
{
	public SeriesBase Series { get; }
	public KyoshinEewViewerConfiguration Config { get; }

	private LandLayer LandLayer { get; } = new();
	private LandBorderLayer LandBorderLayer { get; } = new();
	private GridLayer GridLayer { get; } = new();

	private MapLayer[]? _mapLayers;
	public MapLayer[]? MapLayers
	{
		get => _mapLayers;
		set => this.RaiseAndSetIfChanged(ref _mapLayers, value);
	}

	private MapDisplayParameter _mapDisplayParameter;
	public MapDisplayParameter MapDisplayParameter
	{
		get => _mapDisplayParameter;
		set {
			if (_mapDisplayParameter == value)
				return;
			this.RaiseAndSetIfChanged(ref _mapDisplayParameter, value);
			MapPadding = _mapDisplayParameter.Padding;
			LandBorderLayer.EmphasisMode = _mapDisplayParameter.BorderEmphasis;
			LandBorderLayer.LayerSets = LandLayer.LayerSets = _mapDisplayParameter.LayerSets ?? LandLayerSet.DefaultLayerSets;
			LandLayer.CustomColorMap = _mapDisplayParameter.CustomColorMap;
			UpdateMapLayers();
		}
	}

	private static Thickness BasePadding { get; } = new(0, 0, 0, 0);
	private Thickness _mapPadding = BasePadding;
	public Thickness MapPadding
	{
		get => _mapPadding;
		set => this.RaiseAndSetIfChanged(ref _mapPadding, value);
	}

	private double _maxMapNavigateZoom = 10;
	public double MaxMapNavigateZoom
	{
		get => _maxMapNavigateZoom;
		set => this.RaiseAndSetIfChanged(ref _maxMapNavigateZoom, value);
	}

	public Control? DisplayControl => Series.DisplayControl;

	private IDisposable? MapDisplayParameterListener { get; set; }
	private IDisposable MaxNavigateZoomListener { get; }
	private IDisposable ShowGridListener { get; }
	private IDisposable MapLoadedListener { get; }
	private bool IsDetached { get; set; }

	public event Action? WindowClosed;

	public SeriesWindowViewModel(SeriesBase series, KyoshinEewViewerConfiguration config)
	{
		Series = series;
		Config = config;

		MaxNavigateZoomListener = Config.Map.ObservePropertyChanged(x => x.MaxNavigateZoom).Subscribe(x => MaxMapNavigateZoom = x);
		MaxMapNavigateZoom = Config.Map.MaxNavigateZoom;

		ShowGridListener = Config.Map.ObservePropertyChanged(x => x.ShowGrid).Subscribe(x => UpdateMapLayers());

		MapLoadedListener = MessageBus.Current.Listen<MapLoaded>().Subscribe(e =>
		{
			LandBorderLayer.Map = LandLayer.Map = e.Data;
			UpdateMapLayers();
		});

		if (MapData.CachedMap is { } cachedMap)
		{
			LandBorderLayer.Map = LandLayer.Map = cachedMap;
			UpdateMapLayers();
		}
	}

	public void AttachToSeries()
	{
		MapDisplayParameterListener?.Dispose();
		MapDisplayParameterListener = Series.ObservePropertyChanged(x => x.MapDisplayParameter)
			.Subscribe(x => MapDisplayParameter = x);
		MapDisplayParameter = Series.MapDisplayParameter;

		Series.RecreateDisplayControl();
		this.RaisePropertyChanged(nameof(DisplayControl));

		Series.IsActivated = true;
	}

	public void DetachFromSeries()
	{
		if (IsDetached)
			return;
		IsDetached = true;

		MapDisplayParameterListener?.Dispose();
		MapDisplayParameterListener = null;
		MaxNavigateZoomListener.Dispose();
		ShowGridListener.Dispose();
		MapLoadedListener.Dispose();
		LandBorderLayer.Map = LandLayer.Map = null;
		MapLayers = null;

		Series.IsActivated = false;

		WindowClosed?.Invoke();
	}

	public void ReturnToHomeMap()
	{
		// 自動ナビゲーションが無効な場合はシリーズの範囲ではなくホームポジションに戻す
		var request = Config.Map.AutoFocus ? Series.MapNavigationRequest : null;
		MessageBus.Current.SendMessage(new SeriesWindowMapNavigationRequest(Series, request ?? new MapNavigationRequest(null)));
	}

	private void UpdateMapLayers()
	{
		var layers = new List<MapLayer>();
		if (MapDisplayParameter.BackgroundLayers != null)
			layers.AddRange(MapDisplayParameter.BackgroundLayers);
		layers.Add(LandLayer);
		if (MapDisplayParameter.BaseLayers != null)
			layers.AddRange(MapDisplayParameter.BaseLayers);
		layers.Add(LandBorderLayer);
		if (MapDisplayParameter.OverlayLayers != null)
			layers.AddRange(MapDisplayParameter.OverlayLayers);
		if (Config.Map.ShowGrid)
			layers.Add(GridLayer);
		MapLayers = layers.ToArray();
	}
}

/// <summary>
/// SeriesWindow専用のマップナビゲーションリクエスト
/// </summary>
public record SeriesWindowMapNavigationRequest(SeriesBase Series, MapNavigationRequest? Request);
