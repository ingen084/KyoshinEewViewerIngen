using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers.ImageTile;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
#if !DEBUG
using System.Reflection;
#endif

namespace KyoshinEewViewer.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		[Reactive]
		public string Title { get; set; } = "KyoshinEewViewer for ingen";
		[Reactive]
		public string Version { get; set; } =
#if DEBUG
			"DEBUG";
#else
			Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "不明";
#endif

		[Reactive]
		public double Scale { get; set; } = 1;

		[Reactive]
		public double MaxMapNavigateZoom { get; set; } = 10;

		public ObservableCollection<SeriesBase> Series { get; } = new ObservableCollection<SeriesBase>();

		[Reactive]
		public Thickness MapPadding { get; set; } = BasePadding;
		private static Thickness BasePadding { get; } = new(0, 36, 0, 0);
		private IDisposable? MapPaddingListener { get; set; }

		[Reactive]
		public ImageTileProvider[]? ImageTileProviders { get; protected set; }
		private IDisposable? ImageTileProvidersListener { get; set; }

		[Reactive]
		public IRenderObject[]? RenderObjects { get; protected set; }
		private IDisposable? RenderObjectsListener { get; set; }
		[Reactive]
		public RealtimeRenderObject[]? RealtimeRenderObjects { get; protected set; }
		[Reactive]
		public RealtimeRenderObject[]? StandByRealtimeRenderObjects { get; protected set; }
		private IDisposable? RealtimeRenderObjectsListener { get; set; }

		[Reactive]
		public Dictionary<LandLayerType, Dictionary<int, SKColor>>? CustomColorMap { get; protected set; }
		private IDisposable? CustomColorMapListener { get; set; }

		private IDisposable? FocusPointListener { get; set; }

		private SeriesBase? _selectedSeries;
		public SeriesBase? SelectedSeries
		{
			get => _selectedSeries;
			set {
				if (_selectedSeries == value)
					return;
				// デタッチ
				MapPaddingListener?.Dispose();
				MapPaddingListener = null;
				ImageTileProvidersListener?.Dispose();
				ImageTileProvidersListener = null;
				RenderObjectsListener?.Dispose();
				RenderObjectsListener = null;
				RealtimeRenderObjectsListener?.Dispose();
				RealtimeRenderObjectsListener = null;
				CustomColorMapListener?.Dispose();
				CustomColorMapListener = null;
				FocusPointListener?.Dispose();
				FocusPointListener = null;
				_selectedSeries?.Deactivated();

				value?.Activating();
				this.RaiseAndSetIfChanged(ref _selectedSeries, value);

				// アタッチ
				if (_selectedSeries != null)
				{
					MapPaddingListener = _selectedSeries.WhenAnyValue(x => x.MapPadding).Subscribe(x => MapPadding = x + BasePadding);
					MapPadding = _selectedSeries.MapPadding + BasePadding;

					ImageTileProvidersListener = _selectedSeries.WhenAnyValue(x => x.ImageTileProviders).Subscribe(x => ImageTileProviders = x);
					ImageTileProviders = _selectedSeries.ImageTileProviders;

					RenderObjectsListener = _selectedSeries.WhenAnyValue(x => x.RenderObjects).Subscribe(x => RenderObjects = x);
					RenderObjects = _selectedSeries.RenderObjects;

					RealtimeRenderObjectsListener = _selectedSeries.WhenAnyValue(x => x.RealtimeRenderObjects).Subscribe(x => RealtimeRenderObjects = x);
					RealtimeRenderObjects = _selectedSeries.RealtimeRenderObjects;
					RecalcStandByRealtimeRenderObjects();

					CustomColorMapListener = _selectedSeries.WhenAnyValue(x => x.CustomColorMap).Subscribe(x => CustomColorMap = x);
					CustomColorMap = _selectedSeries.CustomColorMap;

					FocusPointListener = _selectedSeries.WhenAnyValue(x => x.FocusBound).Subscribe(x
						=> MessageBus.Current.SendMessage(new MapNavigationRequested(x)));
					MessageBus.Current.SendMessage(new MapNavigationRequested(_selectedSeries.FocusBound));
				}
				DisplayControl = _selectedSeries?.DisplayControl;
			}
		}
		[Reactive]
		public Control? DisplayControl { get; set; }

		[Reactive]
		public bool UpdateAvailable { get; set; }

		private Rect bounds;
		public Rect Bounds
		{
			get => bounds;
			set {
				bounds = value;
				if (ConfigurationService.Default.Map.KeepRegion)
					MessageBus.Current.SendMessage(new MapNavigationRequested(SelectedSeries?.FocusBound));
			}
		}

		public MainWindowViewModel()
		{
			ConfigurationService.Default.WhenAnyValue(x => x.WindowScale)
				.Subscribe(x => Scale = x);
			if (!Design.IsDesignMode)
				NotificationService.Default.Initalize();

			if (ConfigurationService.Default.KyoshinMonitor.Enabled)
				Series.Add(new KyoshinMonitorSeries());
			if (ConfigurationService.Default.Earthquake.Enabled)
				Series.Add(new EarthquakeSeries());
#if DEBUG
			//Series.Add(new Series.Radar.RadarSeries());
			Series.Add(new Series.Lightning.LightningSeries());
#endif

			if (Design.IsDesignMode)
			{
				UpdateAvailable = true;
				return;
			}

			foreach (var s in Series)
				s.WhenAnyValue(x => x.RealtimeRenderObjects).Subscribe(x => RecalcStandByRealtimeRenderObjects());

			ConfigurationService.Default.Map.WhenAnyValue(x => x.MaxNavigateZoom).Subscribe(x => MaxMapNavigateZoom = x);
			MaxMapNavigateZoom = ConfigurationService.Default.Map.MaxNavigateZoom;

			MessageBus.Current.Listen<UpdateFound>().Subscribe(x => UpdateAvailable = x.FoundUpdate?.Any() ?? false);
			UpdateCheckService.Default.StartUpdateCheckTask();
		}

		private void RecalcStandByRealtimeRenderObjects() => StandByRealtimeRenderObjects = Series
				.Where(s => s != SelectedSeries && s.RealtimeRenderObjects != null)
				.SelectMany(s => s.RealtimeRenderObjects ?? throw new Exception("内部エラー")).ToArray();

		public void RequestNavigate(Rect rect)
			=> MessageBus.Current.SendMessage(new MapNavigationRequested(rect));
	}
}
