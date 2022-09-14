using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.Radar;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

	public ObservableCollection<SeriesBase> Series { get; } = new ObservableCollection<SeriesBase>();

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

	public MapLayer[]? BaseMapLayers { get; set; }
	private IDisposable? BaseMapLayersListener { get; set; }

	public MapLayer[]? OverlayMapLayers { get; set; }
	private IDisposable? OverlayMapLayersListener { get; set; }

	private void UpdateMapLayers()
	{
		var layers = new List<MapLayer>();
		if (LandLayer != null)
			layers.Add(LandLayer);
		if (BaseMapLayers != null)
			layers.AddRange(BaseMapLayers);
		if (LandBorderLayer != null)
			layers.Add(LandBorderLayer);
		if (OverlayMapLayers != null)
			layers.AddRange(OverlayMapLayers);
		if (ConfigurationService.Current.Map.ShowGrid && GridLayer != null)
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
			if (this.RaiseAndSetIfChanged(ref _selectedSeries, value) == oldSeries)
				return;

			lock (_switchSelectLocker)
			{
				// デタッチ
				MapPaddingListener?.Dispose();
				MapPaddingListener = null;

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

	private Rect bounds;
	public Rect Bounds
	{
		get => bounds;
		set {
			bounds = value;
			if (ConfigurationService.Current.Map.KeepRegion)
				MessageBus.Current.SendMessage(new MapNavigationRequested(SelectedSeries?.FocusBound));
		}
	}

	public MainWindowViewModel() : this(null, null) { }
	public MainWindowViewModel(NotificationService? notificationService, TelegramProvideService? telegramProvideService)
	{
		Version = Core.Utils.Version;

		NotificationService = notificationService ?? Locator.Current.GetService<NotificationService>() ?? throw new Exception("notificationServiceの解決に失敗しました");
		TelegramProvideService = telegramProvideService ?? Locator.Current.GetService<TelegramProvideService>() ?? throw new Exception("telegramProvideServiceの解決に失敗しました");
		if (!Design.IsDesignMode)
			NotificationService.Initalize();

		if (Design.IsDesignMode)
		{
			UpdateAvailable = true;
			return;
		}

		ConfigurationService.Current.WhenAnyValue(x => x.WindowScale)
			.Subscribe(x => Scale = x);

		ConfigurationService.Current.Map.WhenAnyValue(x => x.MaxNavigateZoom).Subscribe(x => MaxMapNavigateZoom = x);
		MaxMapNavigateZoom = ConfigurationService.Current.Map.MaxNavigateZoom;

		ConfigurationService.Current.Map.WhenAnyValue(x => x.ShowGrid).Subscribe(x => UpdateMapLayers());

		UpdateCheckService.Default.Updated += x => UpdateAvailable = x?.Any() ?? false;
		UpdateCheckService.Default.StartUpdateCheckTask();

		MessageBus.Current.Listen<ApplicationClosing>().Subscribe(_ =>
		{
			foreach (var s in Series)
				s.Dispose();
		});

		if (StartupOptions.IsStandalone && TryGetStandaloneSeries(StartupOptions.StandaloneSeriesName!, out var sSeries))
		{
			IsStandalone = true;
			SelectedSeries = sSeries;
			NavigationViewPaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
		}
		else
		{

			if (ConfigurationService.Current.KyoshinMonitor.Enabled)
				AddSeries(new KyoshinMonitorSeries(NotificationService, TelegramProvideService));
			if (ConfigurationService.Current.Earthquake.Enabled)
				AddSeries(new EarthquakeSeries(NotificationService, TelegramProvideService));
#if DEBUG
			AddSeries(new Series.Tsunami.TsunamiSeries());
#endif
			if (ConfigurationService.Current.Radar.Enabled)
				AddSeries(new RadarSeries());
#if DEBUG
			AddSeries(new Series.Typhoon.TyphoonSeries());
			AddSeries(new Series.Lightning.LightningSeries());
#endif
			if (ConfigurationService.Current.SelectedTabName != null &&
				Series.FirstOrDefault(s => s.Name == ConfigurationService.Current.SelectedTabName) is SeriesBase ss)
				SelectedSeries = ss;
		}

		Task.Run(async () =>
		{
			var mapData = await MapData.LoadDefaultMapAsync();
			LandBorderLayer.Map = LandLayer.Map = mapData;
			UpdateMapLayers();
			await Task.Delay(500);
			OnMapNavigationRequested(new(SelectedSeries?.FocusBound));
		});

		TelegramProvideService.StartAsync().ConfigureAwait(false);
	}

	private void AddSeries(SeriesBase series)
	{
		series.WhenAnyValue(x => x.Event).Subscribe(x => OnSeriesEvented(series, x));
		Series.Add(series);
	}

	/// <summary>
	/// 発生中のイベント
	/// </summary>
	private List<(SeriesBase, SeriesEvent)> EventStack { get; } = new();
	void OnSeriesEvented(SeriesBase sender, SeriesEvent? e)
	{
		// TODO: 今後実装する
	}

	void OnMapNavigationRequested(MapNavigationRequested? e) => MessageBus.Current.SendMessage(e);

	private static bool TryGetStandaloneSeries(string name, out SeriesBase series)
	{
		switch (name)
		{
			case "kyoshin-monitor":
				series = new KyoshinMonitorSeries();
				return true;
			case "earthquake":
				series = new EarthquakeSeries();
				return true;
			case "radar":
				series = new RadarSeries();
				return true;
			case "lightning":
				series = new Series.Lightning.LightningSeries();
				return true;
			default:
				series = null!;
				return false;
		}
	}

	public void ReturnToHomeMap()
		=> MessageBus.Current.SendMessage(new MapNavigationRequested(SelectedSeries?.FocusBound));

	public void ShowSettingWindow()
		=> SubWindowsService.Default.ShowSettingWindow();
}
