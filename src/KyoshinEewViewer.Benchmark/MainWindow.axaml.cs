using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.Tsunami;
using KyoshinEewViewer.Services;
using R3;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Benchmark
{
	public partial class MainWindow : Window
	{
		private ILogger Logger { get; }

		private LandLayer LandLayer { get; } = new();
		private LandBorderLayer LandBorderLayer { get; } = new();
		private GridLayer GridLayer { get; } = new();

		public KyoshinMonitorSeries KyoshinMonitorSeries { get; }
		public EarthquakeSeries EarthquakeSeries { get; }
		public TsunamiSeries TsunamiSeries { get; }

		public MapLayer[] BackgroundMapLayers => SelectedSeries?.BackgroundMapLayers;
		public MapLayer[] BaseMapLayers => SelectedSeries?.BaseLayers;

		public MapLayer[] OverlayMapLayers => SelectedSeries?.OverlayLayers;

		private void UpdateMapLayers()
		{
			var layers = new List<MapLayer>();
			if (BackgroundMapLayers != null)
				layers.AddRange(BackgroundMapLayers);
			layers.Add(LandLayer);
			if (BaseMapLayers != null)
				layers.AddRange(BaseMapLayers);
			layers.Add(LandBorderLayer);
			if (OverlayMapLayers != null)
				layers.AddRange(OverlayMapLayers);
			if (Config.Map.ShowGrid)
				layers.Add(GridLayer);
			Map.Layers = layers.ToArray();
		}

		public MainWindow()
		{
			Logger = AppLog.Create<MainWindow>();
			Logger.LogInformation("初期化中…");
			InitializeComponent();
			Config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();

			KyoshinMonitorSeries = ServiceLocator.Current.RequireService<KyoshinMonitorSeries>();
			EarthquakeSeries = ServiceLocator.Current.RequireService<EarthquakeSeries>();
			TsunamiSeries = ServiceLocator.Current.RequireService<TsunamiSeries>();
		}

		private KyoshinEewViewerConfiguration Config { get; }

		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);

			if (Design.IsDesignMode)
				return;

			KyoshinMonitorSeries.Initialize();
			EarthquakeSeries.Initialize();
			TsunamiSeries.Initialize();

			ClientSize = new Size(1280, 960);

			Task.Run(() =>
			{
				var mapData = LandBorderLayer.Map = LandLayer.Map = MapData.LoadDefaultMap();
				StrongReferenceMessenger.Default.Send(new MapLoaded(mapData));
				StrongReferenceMessenger.Default.Send(new MapNavigationRequested(SelectedSeries?.FocusBound));
				Logger.LogInformation("マップ読込完了");
			});

			SelectedSeries = KyoshinMonitorSeries;

			ServiceLocator.Current.RequireService<TelegramProvideService>().StartAsync().ConfigureAwait(false);
		}

		protected override void OnClosed(EventArgs e)
		{
			SelectedSeries?.Deactivated();
			KyoshinMonitorSeries?.Dispose();
			EarthquakeSeries?.Dispose();
			TsunamiSeries?.Dispose();
			base.OnClosed(e);
		}

		private IDisposable MapPaddingListener { get; set; }
		private IDisposable BackgroundMapLayersListener { get; set; }
		private IDisposable BaseMapLayersListener { get; set; }
		private IDisposable OverlayMapLayersListener { get; set; }
		private IDisposable CustomColorMapListener { get; set; }
		private IDisposable FocusPointListener { get; set; }

		private readonly object _switchSelectLocker = new();
		private SeriesBase _selectedSeries;
		public SeriesBase SelectedSeries
		{
			get => _selectedSeries;
			set {
				var oldSeries = _selectedSeries;
				if (value == null || _selectedSeries == value)
					return;
				_selectedSeries = value;
				Logger.LogDebug("Series changed: {Name} -> {Name2}", oldSeries?.GetType().Name, _selectedSeries?.GetType().Name);

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

						MapPaddingListener = _selectedSeries.ObservePropertyChanged(x => x.MapPadding).Subscribe(x => Dispatcher.UIThread.Post(() => Map.Padding = x));
						Map.Padding = _selectedSeries.MapPadding;

						BackgroundMapLayersListener = _selectedSeries.ObservePropertyChanged(x => x.BackgroundMapLayers).Subscribe(x => UpdateMapLayers());

						BaseMapLayersListener = _selectedSeries.ObservePropertyChanged(x => x.BaseLayers).Subscribe(x => UpdateMapLayers());

						OverlayMapLayersListener = _selectedSeries.ObservePropertyChanged(x => x.OverlayLayers).Subscribe(x => UpdateMapLayers());

						CustomColorMapListener = _selectedSeries.ObservePropertyChanged(x => x.CustomColorMap).Subscribe(x => LandLayer.CustomColorMap = x);
						LandLayer.CustomColorMap = _selectedSeries.CustomColorMap;

						FocusPointListener = _selectedSeries.ObservePropertyChanged(x => x.FocusBound).Subscribe(x => StrongReferenceMessenger.Default.Send(new MapNavigationRequested(x)));
						StrongReferenceMessenger.Default.Send(new MapNavigationRequested(_selectedSeries.FocusBound));

						_selectedSeries.MapNavigationRequested += OnMapNavigationRequested;

						UpdateMapLayers();
					}
					SeriesContent.Content = _selectedSeries?.DisplayControl;
				}
			}
		}
		private void OnMapNavigationRequested(MapNavigationRequest? e)
			=> StrongReferenceMessenger.Default.Send(e ?? new MapNavigationRequest(null));
	}

	public record CaptureResult(byte[] Data, TimeSpan TotalTime, TimeSpan MeasureTime, TimeSpan ArrangeTime, TimeSpan RenderTime, TimeSpan SaveTime);
}
