using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
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
using KyoshinEewViewer.Series.Qzss;
using KyoshinEewViewer.Series.Radar;
using KyoshinEewViewer.Series.Tsunami;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.Workflows.BuiltinTriggers;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public partial class MainViewModel : ViewModelBase
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
	private IDisposable? MapDisplayParameterListener { get; set; }
	private IDisposable? MapNavigationRequestListener { get; set; }

	private static Thickness BasePadding { get; } = new(0, 0, 0, 0);
	private Thickness _mapPadding = BasePadding;
	public Thickness MapPadding
	{
		get => _mapPadding;
		set => this.RaiseAndSetIfChanged(ref _mapPadding, value);
	}

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
				MapDisplayParameterListener?.Dispose();
				MapDisplayParameterListener = null;

				MapNavigationRequestListener?.Dispose();
				MapNavigationRequestListener = null;

				if (oldSeries != null)
				{
					oldSeries.Deactivated();
					oldSeries.IsActivated = false;
				}

				// アタッチ
				if (_selectedSeries != null)
				{
					_selectedSeries.Activating();
					_selectedSeries.IsActivated = true;

					MapDisplayParameterListener = _selectedSeries.WhenAnyValue(x => x.MapDisplayParameter).Subscribe(x => MapDisplayParameter = x);
					MapNavigationRequestListener = _selectedSeries.WhenAnyValue(x => x.MapNavigationRequest).Subscribe(OnMapNavigationRequested);
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
				MessageBus.Current.SendMessage(SelectedSeries?.MapNavigationRequest ?? new MapNavigationRequest(null));
		}
	}

	private KyoshinEewViewerConfiguration Config { get; }

	public MainViewModel(
		SeriesController? seriesController,
		KyoshinEewViewerConfiguration config,
		UpdateCheckService updateCheckService,
		NotificationService notifyService,
		TelegramProvideService telegramProvideService,
		WorkflowService workflowService,
		VoicevoxService voicevoxService)
	{
		SplatRegistrations.RegisterLazySingleton<MainViewModel>();

		Config = config;

		Version = Utils.Version;
		SeriesController = seriesController ?? throw new ArgumentNullException(nameof(seriesController));

		NotificationService = notifyService;
		TelegramProvideService = telegramProvideService;

		if (Design.IsDesignMode)
		{
			UpdateAvailable = true;
			return;
		}
		NotificationService.Initialize();

		Config.WhenAnyValue(x => x.WindowScale).Subscribe(x => Scale = x);

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
		SeriesController.RegisterSeries(QzssSeries.MetaData);

		//if (StartupOptions.Current?.StandaloneSeriesName is { } ssn && TryGetStandaloneSeries(ssn, out var sSeries))
		//{
		//	IsStandalone = true;
		//	sSeries.Initialize();
		//	SelectedSeries = sSeries;
		//	NavigationViewPaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
		//}
		//else
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

				Dispatcher.UIThread.Post(() => SelectedSeries = s.Series);
			});
		}

		Task.Run(async () =>
		{
			var mapData = MapData.LoadDefaultMap();
			LandBorderLayer.Map = LandLayer.Map = mapData;
			MessageBus.Current.SendMessage(new MapLoaded(mapData));
			UpdateMapLayers();
			await Task.Delay(500);
			OnMapNavigationRequested(SelectedSeries?.MapNavigationRequest ?? new MapNavigationRequest(null));
			workflowService.PublishEvent(new ApplicationStartupEvent());
		});

		TelegramProvideService.StartAsync().ConfigureAwait(false);

		workflowService.LoadWorkflows();

		if (config.Voicevox.Enabled)
			voicevoxService.GetSpeakers().ConfigureAwait(false);
	}

	private void OnMapNavigationRequested(MapNavigationRequest? e) => MessageBus.Current.SendMessage(e);

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
		=> MessageBus.Current.SendMessage(SelectedSeries?.MapNavigationRequest ?? new MapNavigationRequest(null));

	public void ShowSettingWindow()
		=> MessageBus.Current.SendMessage(new ShowSettingWindowRequested());
}
