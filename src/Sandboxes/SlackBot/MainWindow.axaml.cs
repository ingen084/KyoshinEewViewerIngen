using Avalonia;
using Avalonia.Controls;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using Avalonia.Threading;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.KyoshinMonitor.Events;
using KyoshinEewViewer.Series.Tsunami;
using KyoshinEewViewer.Series.Tsunami.Events;
using KyoshinEewViewer.Services;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ILogger = Splat.ILogger;

namespace SlackBot
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

		public MapLayer[]? BackgroundMapLayers => SelectedSeries?.BackgroundMapLayers;
		public MapLayer[]? BaseMapLayers => SelectedSeries?.BaseLayers;

		public MapLayer[]? OverlayMapLayers => SelectedSeries?.OverlayLayers;

		public SlackUploader? SlackUploader { get; }
		public MisskeyUploader MisskeyUploader { get; } = new();

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

		private SKBitmap Bitmap { get; }
		private SKCanvas Canvas { get; }

		public MainWindow()
		{
			Logger = Locator.Current.RequireService<ILogManager>().GetLogger<MainWindow>();
			Logger.LogInfo("初期化中…");
			InitializeComponent();
			Config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();

			KyoshinMonitorSeries = Locator.Current.RequireService<KyoshinMonitorSeries>();
			EarthquakeSeries = Locator.Current.RequireService<EarthquakeSeries>();
			TsunamiSeries = Locator.Current.RequireService<TsunamiSeries>();

			KyoshinEewViewerApp.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
					.Subscribe(x => Map.RefreshResourceCache(x!.Theme));

			// キャプチャ用のメモリ確保 端数は切り捨て
			Bitmap = new SKBitmap((int)Math.Floor(1280 * Config.WindowScale), (int)Math.Floor(720 * Config.WindowScale));
			Canvas = new SKCanvas(Bitmap);

			if (Environment.GetEnvironmentVariable("SLACK_API_TOKEN") is { } slackApiToken && Environment.GetEnvironmentVariable("SLACK_CHANNEL_ID") is { } slackChannelId)
				SlackUploader = new SlackUploader(slackApiToken, slackChannelId);
			else
				Logger.LogWarning("環境変数 SLACK_API_TOKEN または SLACK_CHANNEL_ID が設定されていないため、Slackへの投稿ができません。");
		}

		public ManualResetEventSlim Mres { get; } = new(true);
		private KyoshinEewViewerConfiguration Config { get; }

		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);

			if (Design.IsDesignMode)
				return;

			KyoshinMonitorSeries.Initialize();
			EarthquakeSeries.Initialize();
			TsunamiSeries.Initialize();

			ClientSize = new Size(1280, 720);

			Task.Run(() =>
			{
				var mapData = LandBorderLayer.Map = LandLayer.Map = MapData.LoadDefaultMap();
				MessageBus.Current.SendMessage(new MapLoaded(mapData));
				MessageBus.Current.SendMessage(new MapNavigationRequested(SelectedSeries?.FocusBound));
				Logger.LogInfo("マップ読込完了");
			});

			MessageBus.Current.Listen<MapNavigationRequested>().Subscribe(x =>
			{
				Logger.LogInfo($"地図移動: {x.Bound}");
				if (x.Bound is { } rect)
				{
					if (x.MustBound is { } mustBound)
						Map.Navigate(rect, TimeSpan.Zero, mustBound);
					else
						Map.Navigate(rect, TimeSpan.Zero);
				}
				else
					NavigateToHome();
			});

			MessageBus.Current.Listen<KyoshinShakeDetected>().Subscribe(async x =>
			{
				// 震度1未満の揺れは処理しない
				if (x.Event.Level <= KyoshinEventLevel.Weaker)
					return;

				if (!Mres.IsSet)
					await Task.Run(() => Mres.Wait());
				Mres.Reset();
				try
				{
					await Dispatcher.UIThread.InvokeAsync(() => SelectedSeries = KyoshinMonitorSeries);
					var captureTask = CaptureImageAsync();
					var channel = Channel.CreateBounded<string?>(1);
					await Task.WhenAll(
						MisskeyUploader.UploadShakeDetected(x, captureTask, channel),
						SlackUploader?.UploadShakeDetected(x, channel) ?? Task.CompletedTask
					);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "揺れ検知情報投稿時に例外が発生しました");
				}
				finally
				{
					Mres.Set();
				}
			});

			SelectedSeries = KyoshinMonitorSeries;

			MessageBus.Current.Listen<EarthquakeInformationUpdated>().Subscribe(async x =>
			{
				if (!Mres.IsSet)
					await Task.Run(() => Mres.Wait());
				Mres.Reset();
				try
				{
					await Dispatcher.UIThread.InvokeAsync(() => SelectedSeries = EarthquakeSeries);
					var captureTask = CaptureImageAsync();
					var channel = Channel.CreateBounded<string?>(1);
					await Task.WhenAll(
						MisskeyUploader.UploadEarthquakeInformation(x, captureTask, channel),
						SlackUploader?.UploadEarthquakeInformation(x, channel) ?? Task.CompletedTask
					);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "地震情報投稿時に例外が発生しました");
				}
				finally
				{
					Mres.Set();
				}
			});

			MessageBus.Current.Listen<TsunamiInformationUpdated>().Subscribe(async x =>
			{
				if (!Mres.IsSet)
					await Task.Run(() => Mres.Wait());

				Mres.Reset();
				try
				{
					await Dispatcher.UIThread.InvokeAsync(() => SelectedSeries = TsunamiSeries);
					var captureTask = CaptureImageAsync();
					var channel = Channel.CreateBounded<string?>(1);
					await Task.WhenAll(
						MisskeyUploader.UploadTsunamiInformation(x, captureTask, channel),
						SlackUploader?.UploadTsunamiInformation(x, channel) ?? Task.CompletedTask
					);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "津波情報投稿時に例外が発生しました");
				}
				finally
				{
					Mres.Set();
				}
			});

			Locator.Current.RequireService<TelegramProvideService>().StartAsync().ConfigureAwait(false);

#if DEBUG
			//Task.Run(async () =>
			//{
			//	await Task.Delay(5000);
			//	//Dispatcher.UIThread.Invoke(() => SelectedSeries = EarthquakeSeries);
			//	await MisskeyUploader.UploadTest(Task.Run(CaptureImage));
			//	//await SlackUploader.Upload(
			//	//	null,
			//	//	"#FFF",
			//	//	"テスト1",
			//	//	"テストメッセージ1",
			//	//	captureTask: Task.Run(CaptureImage)
			//	//);
			//	//await Task.Delay(5000);
			//	//Dispatcher.UIThread.Invoke(() => SelectedSeries = KyoshinMonitorSeries);
			//	//await Task.Delay(1000);
			//	//await SlackUploader.Upload(
			//	//	null,
			//	//	"#FFF",
			//	//	"テスト2",
			//	//	"テストメッセージ2",
			//	//	captureTask: Task.Run(CaptureImage)
			//	//);
			//});
#endif
		}

		private void NavigateToHome()
			=> Map.Navigate(new RectD(Config.Map.Location1.CastPoint(), Config.Map.Location2.CastPoint()), TimeSpan.Zero);

		protected override void OnClosed(EventArgs e)
		{
			SelectedSeries?.Deactivated();
			KyoshinMonitorSeries?.Dispose();
			EarthquakeSeries?.Dispose();
			TsunamiSeries?.Dispose();
			base.OnClosed(e);
		}

		private IDisposable? MapPaddingListener { get; set; }
		private IDisposable? BackgroundMapLayersListener { get; set; }
		private IDisposable? BaseMapLayersListener { get; set; }
		private IDisposable? OverlayMapLayersListener { get; set; }
		private IDisposable? CustomColorMapListener { get; set; }
		private IDisposable? FocusPointListener { get; set; }

		private readonly object _switchSelectLocker = new();
		private SeriesBase? _selectedSeries;
		public SeriesBase? SelectedSeries
		{
			get => _selectedSeries;
			set {
				var oldSeries = _selectedSeries;
				if (value == null || _selectedSeries == value)
					return;
				_selectedSeries = value;
				Logger.LogDebug($"Series changed: {oldSeries?.GetType().Name} -> {_selectedSeries?.GetType().Name}");

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

						MapPaddingListener = _selectedSeries.WhenAnyValue(x => x.MapPadding).Subscribe(x => Dispatcher.UIThread.Post(() => Map.Padding = x));
						Map.Padding = _selectedSeries.MapPadding;

						BackgroundMapLayersListener = _selectedSeries.WhenAnyValue(x => x.BackgroundMapLayers).Subscribe(x => UpdateMapLayers());

						BaseMapLayersListener = _selectedSeries.WhenAnyValue(x => x.BaseLayers).Subscribe(x => UpdateMapLayers());

						OverlayMapLayersListener = _selectedSeries.WhenAnyValue(x => x.OverlayLayers).Subscribe(x => UpdateMapLayers());

						CustomColorMapListener = _selectedSeries.WhenAnyValue(x => x.CustomColorMap).Subscribe(x => LandLayer.CustomColorMap = x);
						LandLayer.CustomColorMap = _selectedSeries.CustomColorMap;

						FocusPointListener = _selectedSeries.WhenAnyValue(x => x.FocusBound).Subscribe(x => MessageBus.Current.SendMessage(new MapNavigationRequested(x)));
						MessageBus.Current.SendMessage(new MapNavigationRequested(_selectedSeries.FocusBound));

						_selectedSeries.MapNavigationRequested += OnMapNavigationRequested;

						UpdateMapLayers();
					}
					SeriesContent.Content = _selectedSeries?.DisplayControl;
				}
			}
		}
		private void OnMapNavigationRequested(MapNavigationRequested? e) => MessageBus.Current.SendMessage(e);


		public async Task<CaptureResult> CaptureImageAsync()
		{
			if (!Dispatcher.UIThread.CheckAccess())
				return await Dispatcher.UIThread.InvokeAsync(CaptureImageAsync, DispatcherPriority.SystemIdle); // 優先度を下げないと画面更新前にキャプチャしてしまう

			var sw = Stopwatch.StartNew();
			var size = new Size(ClientSize.Width, ClientSize.Height);
			Measure(size);
			var measure = sw.Elapsed;
			Arrange(new Rect(size));
			var arrange = sw.Elapsed;
			await DrawingContextHelper.RenderAsync(Canvas, this, Bounds, SkiaPlatform.DefaultDpi * Config.WindowScale);
			var render = sw.Elapsed;

			using var stream = new MemoryStream();
			using (var data = Bitmap.Encode(SKEncodedImageFormat.Webp, 100))
				data.SaveTo(stream);
			var save = sw.Elapsed;

			Logger.LogInfo($"Total: {save.TotalMilliseconds}ms Measure: {measure.TotalMilliseconds}ms Arrange: {(arrange - measure).TotalMilliseconds}ms Render: {(render - arrange - measure).TotalMilliseconds}ms Save: {(save - render - arrange - measure).TotalMilliseconds}ms");
			return new CaptureResult(stream.ToArray(), save, measure, arrange - measure, render - arrange - measure, save - render - arrange - measure);
		}
		public async Task CaptureImageAsync(Stream outputStream)
		{
			if (!Dispatcher.UIThread.CheckAccess())
			{
				await Dispatcher.UIThread.InvokeAsync(() => CaptureImageAsync(outputStream), DispatcherPriority.SystemIdle); // 優先度を下げないと画面更新前にキャプチャしてしまう
				return;
			}

			var sw = Stopwatch.StartNew();
			var size = new Size(ClientSize.Width, ClientSize.Height);
			Measure(size);
			var measure = sw.Elapsed;
			Arrange(new Rect(size));
			var arrange = sw.Elapsed;
			await DrawingContextHelper.RenderAsync(Canvas, this, Bounds, SkiaPlatform.DefaultDpi * Config.WindowScale);
			var render = sw.Elapsed;

			using (var data = Bitmap.Encode(SKEncodedImageFormat.Webp, 100))
				await Task.Run(() => data.SaveTo(outputStream));
			var save = sw.Elapsed;

			Logger.LogInfo($"Total: {save.TotalMilliseconds}ms Measure: {measure.TotalMilliseconds}ms Arrange: {(arrange - measure).TotalMilliseconds}ms Render: {(render - arrange - measure).TotalMilliseconds}ms Save: {(save - render - arrange - measure).TotalMilliseconds}ms");
		}
	}

	public record CaptureResult(byte[] Data, TimeSpan TotalTime, TimeSpan MeasureTime, TimeSpan ArrangeTime, TimeSpan RenderTime, TimeSpan SaveTime);
}
