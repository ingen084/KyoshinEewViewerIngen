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

		public MapLayer[] BackgroundMapLayers => SelectedSeries?.MapDisplayParameter.BackgroundLayers;
		public MapLayer[] BaseMapLayers => SelectedSeries?.MapDisplayParameter.BaseLayers;

		public MapLayer[] OverlayMapLayers => SelectedSeries?.MapDisplayParameter.OverlayLayers;

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
			KyoshinMonitorSeries.RecreateDisplayControl();
			EarthquakeSeries.Initialize();
			EarthquakeSeries.RecreateDisplayControl();
			TsunamiSeries.Initialize();
			TsunamiSeries.RecreateDisplayControl();

			ClientSize = new Size(1280, 960);

			Task.Run(() =>
			{
				var mapData = LandBorderLayer.Map = LandLayer.Map = MapData.LoadDefaultMap();
				StrongReferenceMessenger.Default.Send(new MapLoaded(mapData));
				StrongReferenceMessenger.Default.Send(SelectedSeries?.MapNavigationRequest ?? new MapNavigationRequest(null));
				Logger.LogInformation("マップ読込完了");
			});

			SelectedSeries = KyoshinMonitorSeries;

			ServiceLocator.Current.RequireService<TelegramProvideService>().StartAsync().ConfigureAwait(false);
		}

		protected override void OnClosed(EventArgs e)
		{
			KyoshinMonitorSeries?.Dispose();
			EarthquakeSeries?.Dispose();
			TsunamiSeries?.Dispose();
			base.OnClosed(e);
		}

		private IDisposable MapDisplayParameterListener { get; set; }
		private IDisposable MapNavigationRequestListener { get; set; }

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
					MapDisplayParameterListener?.Dispose();
					MapDisplayParameterListener = null;

					MapNavigationRequestListener?.Dispose();
					MapNavigationRequestListener = null;

					// アタッチ
					if (_selectedSeries != null)
					{
						MapDisplayParameterListener = _selectedSeries.ObservePropertyChanged(x => x.MapDisplayParameter).SubscribeLoggingErrors(x =>
						{
							Dispatcher.UIThread.Post(() => Map.Padding = x.Padding);
							LandLayer.CustomColorMap = x.CustomColorMap;
							UpdateMapLayers();
						});
						Map.Padding = _selectedSeries.MapDisplayParameter.Padding;
						LandLayer.CustomColorMap = _selectedSeries.MapDisplayParameter.CustomColorMap;

						MapNavigationRequestListener = _selectedSeries.ObservePropertyChanged(x => x.MapNavigationRequest).SubscribeLoggingErrors(OnMapNavigationRequested);
						OnMapNavigationRequested(_selectedSeries.MapNavigationRequest);

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
