using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.Radar;
using KyoshinEewViewer.Series.Tsunami;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	public string Title { get; } = "KyoshinEewViewer for ingen";

	private string _version = "?";
	public string Version
	{
		get => _version;
		set => this.RaiseAndSetIfChanged(ref _version, value);
	}

	private double _scale = 1;
	public double Scale
	{
		get => _scale;
		set => this.RaiseAndSetIfChanged(ref _scale, value);
	}

	private double _maxMapNavigateZoom = 10;
	public double MaxMapNavigateZoom
	{
		get => _maxMapNavigateZoom;
		set => this.RaiseAndSetIfChanged(ref _maxMapNavigateZoom, value);
	}

	public SeriesController SeriesController { get; }

	private Thickness _mapPadding = BasePadding;
	public Thickness MapPadding
	{
		get => _mapPadding;
		set => this.RaiseAndSetIfChanged(ref _mapPadding, value);
	}
	private static Thickness BasePadding { get; } = new(0, 0, 0, 0);
	private IDisposable? MapPaddingListener { get; set; }

	private NavigationViewPaneDisplayMode _navigationViewPaneDisplayMode = NavigationViewPaneDisplayMode.Left;
	public NavigationViewPaneDisplayMode NavigationViewPaneDisplayMode
	{
		get => _navigationViewPaneDisplayMode;
		set => this.RaiseAndSetIfChanged(ref _navigationViewPaneDisplayMode, value);
	}

	private LandLayer LandLayer { get; } = new();
	private LandBorderLayer LandBorderLayer { get; } = new();
	private GridLayer GridLayer { get; } = new();

	private MapLayer[]? _mapLayers;
	public MapLayer[]? MapLayers
	{
		get => _mapLayers;
		set => this.RaiseAndSetIfChanged(ref _mapLayers, value);
	}

	public MapLayer[]? BackgroundMapLayers { get; set; }
	private IDisposable? BackgroundMapLayersListener { get; set; }

	public MapLayer[]? BaseMapLayers { get; set; }
	private IDisposable? BaseMapLayersListener { get; set; }

	public MapLayer[]? OverlayMapLayers { get; set; }
	private IDisposable? OverlayMapLayersListener { get; set; }

	private void UpdateMapLayers()
	{
		var layers = new List<MapLayer>();
		if (BackgroundMapLayers != null)
			layers.AddRange(BackgroundMapLayers);
		if (LandLayer != null)
			layers.Add(LandLayer);
		if (BaseMapLayers != null)
			layers.AddRange(BaseMapLayers);
		if (LandBorderLayer != null)
			layers.Add(LandBorderLayer);
		if (OverlayMapLayers != null)
			layers.AddRange(OverlayMapLayers);
		if (Config.Map.ShowGrid && GridLayer != null)
			layers.Add(GridLayer);
		MapLayers = layers.ToArray();
	}

	private IDisposable? CustomColorMapListener { get; set; }

	private IDisposable? FocusPointListener { get; set; }

	private readonly object _switchSelectLocker = new();
	private SeriesBase? _selectedSeries;
	public SeriesBase? SelectedSeries
	{
		get => _selectedSeries;
		set {
			var oldSeries = _selectedSeries;
			if (value == null || this.RaiseAndSetIfChanged(ref _selectedSeries, value) == oldSeries)
				return;
			Debug.WriteLine($"Series changed: {oldSeries?.GetType().Name} -> {_selectedSeries?.GetType().Name}");

			lock (_switchSelectLocker)
			{
				// デタッチ
				MapPaddingListener?.Dispose();
				MapPaddingListener = null;

				BackgroundMapLayersListener?.Dispose();
				BackgroundMapLayersListener = null;

				BaseMapLayersListener?.Dispose();
				BaseMapLayersListener = null;

				OverlayMapLayersListener?.Dispose();
				OverlayMapLayersListener = null;

				CustomColorMapListener?.Dispose();
				CustomColorMapListener = null;

				FocusPointListener?.Dispose();
				FocusPointListener = null;

				if (oldSeries != null)
				{
					oldSeries.MapNavigationRequested -= OnMapNavigationRequested;
					oldSeries.Deactivated();
					oldSeries.IsActivated = false;
				}

				// アタッチ
				if (_selectedSeries != null)
				{
					_selectedSeries.Activating();
					_selectedSeries.IsActivated = true;

					MapPaddingListener = _selectedSeries.WhenAnyValue(x => x.MapPadding).Subscribe(x => MapPadding = x + BasePadding);
					MapPadding = _selectedSeries.MapPadding + BasePadding;

					BackgroundMapLayersListener = _selectedSeries.WhenAnyValue(x => x.BackgroundMapLayers).Subscribe(x => { BaseMapLayers = x; UpdateMapLayers(); });
					BackgroundMapLayers = _selectedSeries.BackgroundMapLayers;

					BaseMapLayersListener = _selectedSeries.WhenAnyValue(x => x.BaseLayers).Subscribe(x => { BaseMapLayers = x; UpdateMapLayers(); });
					BaseMapLayers = _selectedSeries.BaseLayers;

					OverlayMapLayersListener = _selectedSeries.WhenAnyValue(x => x.OverlayLayers).Subscribe(x => { OverlayMapLayers = x; UpdateMapLayers(); });
					OverlayMapLayers = _selectedSeries.OverlayLayers;

					CustomColorMapListener = _selectedSeries.WhenAnyValue(x => x.CustomColorMap).Subscribe(x => LandLayer.CustomColorMap = x);
					LandLayer.CustomColorMap = _selectedSeries.CustomColorMap;

					FocusPointListener = _selectedSeries.WhenAnyValue(x => x.FocusBound).Subscribe(x => MessageBus.Current.SendMessage(new MapNavigationRequested(x)));
					MessageBus.Current.SendMessage(new MapNavigationRequested(_selectedSeries.FocusBound));

					_selectedSeries.MapNavigationRequested += OnMapNavigationRequested;

					UpdateMapLayers();
				}
				DisplayControl = _selectedSeries?.DisplayControl;
			}
		}
	}

	private Control? _displayControl;
	public Control? DisplayControl
	{
		get => _displayControl;
		set => this.RaiseAndSetIfChanged(ref _displayControl, value);
	}

	private bool _isStandalone;
	public bool IsStandalone
	{
		get => _isStandalone;
		set => this.RaiseAndSetIfChanged(ref _isStandalone, value);
	}

	private bool _updateAvailable;
	public bool UpdateAvailable
	{
		get => _updateAvailable;
		set => this.RaiseAndSetIfChanged(ref _updateAvailable, value);
	}

	private NotificationService NotificationService { get; }
	private TelegramProvideService TelegramProvideService { get; }

	private Rect _bounds;
	public Rect Bounds
	{
		get => _bounds;
		set {
			_bounds = value;
			if (Config.Map.KeepRegion)
				MessageBus.Current.SendMessage(new MapNavigationRequested(SelectedSeries?.FocusBound));
		}
	}

	private KyoshinEewViewerConfiguration Config { get; }
	private SubWindowsService SubWindowsService { get; }

	public MainWindowViewModel(SeriesController? seriesController, KyoshinEewViewerConfiguration config, SubWindowsService subWindowsService, UpdateCheckService updateCheckService, NotificationService notifyService, TelegramProvideService telegramProvideService)
	{
		SplatRegistrations.RegisterLazySingleton<MainWindowViewModel>();

		Config = config;
		SubWindowsService = subWindowsService;

		Version = Utils.Version;
		SeriesController = seriesController ?? throw new ArgumentNullException(nameof(seriesController));

		NotificationService = notifyService;
		TelegramProvideService = telegramProvideService;
		if (!Design.IsDesignMode)
			NotificationService.Initialize();

		if (Design.IsDesignMode)
		{
			UpdateAvailable = true;
			return;
		}

		Config.WhenAnyValue(x => x.WindowScale)
			.Subscribe(x => Scale = x);

		Config.Map.WhenAnyValue(x => x.MaxNavigateZoom).Subscribe(x => MaxMapNavigateZoom = x);
		MaxMapNavigateZoom = Config.Map.MaxNavigateZoom;

		Config.Map.WhenAnyValue(x => x.ShowGrid).Subscribe(x => UpdateMapLayers());

		updateCheckService.Updated += x => UpdateAvailable = x?.Any() ?? false;
		updateCheckService.StartUpdateCheckTask();

		MessageBus.Current.Listen<ApplicationClosing>().Subscribe(_ =>
		{
			foreach (var s in SeriesController.EnabledSeries)
				s.Dispose();
		});

		SeriesController.RegisterSeries(KyoshinMonitorSeries.MetaData);
		SeriesController.RegisterSeries(EarthquakeSeries.MetaData);
		SeriesController.RegisterSeries(TsunamiSeries.MetaData);
		SeriesController.RegisterSeries(RadarSeries.MetaData);

#if DEBUG
		SeriesController.RegisterSeries(Series.Typhoon.TyphoonSeries.MetaData);
		SeriesController.RegisterSeries(Series.Lightning.LightningSeries.MetaData);
#endif

		if (StartupOptions.Current?.StandaloneSeriesName is { } ssn && TryGetStandaloneSeries(ssn, out var sSeries))
		{
			IsStandalone = true;
			sSeries.Initialize();
			SelectedSeries = sSeries;
			NavigationViewPaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
		}
		else
		{
			SeriesController.InitializeSeries(Config);

			if (Config.SelectedTabName != null &&
				SeriesController.EnabledSeries.FirstOrDefault(s => s.Meta.Key == Config.SelectedTabName) is { } ss)
				SelectedSeries = ss;

			SelectedSeries ??= SeriesController.EnabledSeries.FirstOrDefault();

			MessageBus.Current.Listen<ActiveRequest>().Subscribe(s =>
			{
				if (s.Series == SelectedSeries)
					return;
				SelectedSeries = s.Series;
			});
		}

		Task.Run(async () =>
		{
			var mapData = await MapData.LoadDefaultMapAsync();
			LandBorderLayer.Map = LandLayer.Map = mapData;
			MessageBus.Current.SendMessage(new MapLoaded(mapData));
			UpdateMapLayers();
			await Task.Delay(1000);
			OnMapNavigationRequested(new(SelectedSeries?.FocusBound));
		});

		TelegramProvideService.StartAsync().ConfigureAwait(false);
	}

	private void OnMapNavigationRequested(MapNavigationRequested? e) => MessageBus.Current.SendMessage(e);

	private bool TryGetStandaloneSeries(string name, out SeriesBase series)
	{
		var meta = SeriesController.AllSeries.FirstOrDefault(s => s.Key == name);
		if (meta == null)
		{
			series = null!;
			return false;
		}
		if (Locator.Current.GetService(meta.Type) is not SeriesBase s)
		{
			series = null!;
			return false;
		}
		series = s;
		return true;
	}

	public void ReturnToHomeMap()
		=> MessageBus.Current.SendMessage(new MapNavigationRequested(SelectedSeries?.FocusBound));

	public void ShowSettingWindow()
		=> SubWindowsService.ShowSettingWindow();
}
