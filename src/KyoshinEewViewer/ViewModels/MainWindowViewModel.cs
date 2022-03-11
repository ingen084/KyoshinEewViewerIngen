using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.Radar;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
	[Reactive]
	public string Title { get; set; } = "KyoshinEewViewer for ingen";
	[Reactive]
	public string Version { get; set; }

	[Reactive]
	public double Scale { get; set; } = 1;

	[Reactive]
	public double MaxMapNavigateZoom { get; set; } = 10;

	public ObservableCollection<SeriesBase> Series { get; } = new ObservableCollection<SeriesBase>();

	[Reactive]
	public Thickness MapPadding { get; set; } = BasePadding;
	private static Thickness BasePadding { get; } = new(0, 36, 0, 0);
	private IDisposable? MapPaddingListener { get; set; }


	private LandLayer LandLayer { get; } = new();
	private LandBorderLayer LandBorderLayer { get; } = new();
	private GridLayer GridLayer { get; } = new();

	[Reactive]
	public MapLayer[]? MapLayers { get; set; }

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
			if (_selectedSeries == value)
				return;

			lock (_switchSelectLocker)
			{
				// �f�^�b�`
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

				_selectedSeries?.Deactivated();

				value?.Activating();
				this.RaiseAndSetIfChanged(ref _selectedSeries, value);

				// �A�^�b�`
				if (_selectedSeries != null)
				{
					MapPaddingListener = _selectedSeries.WhenAnyValue(x => x.MapPadding).Subscribe(x => MapPadding = x + BasePadding);
					MapPadding = _selectedSeries.MapPadding + BasePadding;

					BaseMapLayersListener = _selectedSeries.WhenAnyValue(x => x.BaseLayers).Subscribe(x => { BaseMapLayers = x; UpdateMapLayers(); });
					BaseMapLayers = _selectedSeries.BaseLayers;

					OverlayMapLayersListener = _selectedSeries.WhenAnyValue(x => x.OverlayLayers).Subscribe(x => { OverlayMapLayers = x; UpdateMapLayers(); });
					OverlayMapLayers = _selectedSeries.OverlayLayers;

					CustomColorMapListener = _selectedSeries.WhenAnyValue(x => x.CustomColorMap).Subscribe(x => LandLayer.CustomColorMap = x);
					LandLayer.CustomColorMap = _selectedSeries.CustomColorMap;

					FocusPointListener = _selectedSeries.WhenAnyValue(x => x.FocusBound).Subscribe(x
						=> MessageBus.Current.SendMessage(new MapNavigationRequested(x)));
					MessageBus.Current.SendMessage(new MapNavigationRequested(_selectedSeries.FocusBound));

					UpdateMapLayers();
				}
				DisplayControl = _selectedSeries?.DisplayControl;
			}
		}
	}
	[Reactive]
	public Control? DisplayControl { get; set; }

	[Reactive]
	public bool IsStandalone { get; set; }

	[Reactive]
	public bool UpdateAvailable { get; set; }

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

		if (StartupOptions.IsStandalone && TryGetStandaloneSeries(StartupOptions.StandaloneSeriesName!, out var sSeries))
		{
			IsStandalone = true;
			SelectedSeries = sSeries;
		}
		else
		{

			if (ConfigurationService.Current.KyoshinMonitor.Enabled)
				Series.Add(new KyoshinMonitorSeries(NotificationService));
			if (ConfigurationService.Current.Earthquake.Enabled)
				Series.Add(new EarthquakeSeries(NotificationService, TelegramProvideService));
			if (ConfigurationService.Current.Radar.Enabled)
				Series.Add(new RadarSeries());
#if DEBUG
			Series.Add(new Series.Typhoon.TyphoonSeries());
			Series.Add(new Series.Lightning.LightningSeries());
#endif
			if (ConfigurationService.Current.SelectedTabName != null &&
				Series.FirstOrDefault(s => s.Name == ConfigurationService.Current.SelectedTabName) is SeriesBase ss)
				SelectedSeries = ss;
		}

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

		Task.Run(async () =>
		{
			var mapData = await MapData.LoadDefaultMapAsync();
			LandBorderLayer.Map = LandLayer.Map = mapData;
			UpdateMapLayers();
		});

		TelegramProvideService.StartAsync().ConfigureAwait(false);
	}

	private bool TryGetStandaloneSeries(string name, out SeriesBase series)
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
}
