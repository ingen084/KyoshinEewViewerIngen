using Avalonia;
using Avalonia.Controls;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
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
using R3;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SlackBot
{
	public partial class MainWindow : Window
	{
		private ILogger Logger { get; }

		private LandLayer LandLayer { get; } = new();
		private LandBorderLayer LandBorderLayer { get; } = new();
		private GridLayer GridLayer { get; } = new();

		private LandLayer MiniMapLandLayer { get; } = new();
		private LandBorderLayer MiniMapLandBorderLayer { get; } = new();

		public KyoshinMonitorSeries KyoshinMonitorSeries { get; }
		public EarthquakeSeries EarthquakeSeries { get; }
		public TsunamiSeries TsunamiSeries { get; }

		public MapLayer[]? BackgroundMapLayers => SelectedSeries?.MapDisplayParameter.BackgroundLayers;
		public MapLayer[]? BaseMapLayers => SelectedSeries?.MapDisplayParameter.BaseLayers;

		public MapLayer[]? OverlayMapLayers => SelectedSeries?.MapDisplayParameter.OverlayLayers;

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
			Logger = AppLog.Create<MainWindow>();
			Logger.LogInformation("初期化中…");
			InitializeComponent();
			Config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();
			MainLayout.LayoutTransform = new Avalonia.Media.ScaleTransform(Config.WindowScale, Config.WindowScale);

			KyoshinMonitorSeries = ServiceLocator.Current.RequireService<KyoshinMonitorSeries>();
			EarthquakeSeries = ServiceLocator.Current.RequireService<EarthquakeSeries>();
			TsunamiSeries = ServiceLocator.Current.RequireService<TsunamiSeries>();

			KyoshinEewViewerApp.Selector?.ObservePropertyChanged(x => x.SelectedWindowTheme).Where(x => x != null)
					.Subscribe(x =>
					{
						Map.RefreshResourceCache(x!.Theme);
						MiniMap.RefreshResourceCache(x!.Theme);
					});

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
			KyoshinMonitorSeries.RecreateDisplayControl();
			EarthquakeSeries.Initialize();
			EarthquakeSeries.RecreateDisplayControl();
			TsunamiSeries.Initialize();
			TsunamiSeries.RecreateDisplayControl();

			ClientSize = new Size(1280 * Config.WindowScale, 720 * Config.WindowScale);

			Task.Run(() =>
			{
				var mapData = LandBorderLayer.Map = LandLayer.Map = MapData.LoadDefaultMap();
				MiniMapLandBorderLayer.Map = MiniMapLandLayer.Map = mapData;
				StrongReferenceMessenger.Default.Send(new MapLoaded(mapData));
				StrongReferenceMessenger.Default.Send(SelectedSeries?.MapNavigationRequest ?? new MapNavigationRequest(null));
				Logger.LogInformation("マップ読込完了");
				Dispatcher.UIThread.Post(UpdateMiniMapLayers);
				Dispatcher.UIThread.Post(ResetMiniMapPosition);
			});

			Observable.CombineLatest(
					Map.ObservePropertyChanged(m => m.CenterLocation).AsUnitObservable(),
					Map.ObservePropertyChanged(m => m.Zoom).AsUnitObservable())
				.Subscribe(_ =>
			{
				Dispatcher.UIThread.Post(() =>
				{
					MiniMapContainer.IsVisible = Config.Map.UseMiniMap && Map.IsNavigatedPosition(new RectD(Config.Map.Location1.CastPoint(), Config.Map.Location2.CastPoint()));
					ResetMiniMapPosition();
				});
			});

			MiniMap.ObservePropertyChanged(m => m.Bounds).Subscribe(_ => ResetMiniMapPosition());

			StrongReferenceMessenger.Default.Register<MapNavigationRequest>(this, (_, x) =>
			{
				Logger.LogInformation("地図移動: {Bound}", x?.Bound);
				if (x?.Bound is { } rect)
				{
					if (x.MustBound is { } mustBound)
						Map.Navigate(rect, TimeSpan.Zero, mustBound);
					else
						Map.Navigate(rect, TimeSpan.Zero);
				}
				else
					NavigateToHome();
			});

			StrongReferenceMessenger.Default.Register<KyoshinShakeDetected>(this, async (_, x) =>
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
					var imageUrlSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
					await Task.WhenAll(
						MisskeyUploader.UploadShakeDetected(x, captureTask, imageUrlSource),
						SlackUploader?.UploadShakeDetected(x, imageUrlSource) ?? Task.CompletedTask
					).WaitAsync(TimeSpan.FromSeconds(10));
				}
				catch (TimeoutException)
				{
					Logger.LogWarning("揺れ検知情報の投稿が10秒以内に完了しませんでした。次のイベント受付を再開します。");
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

			StrongReferenceMessenger.Default.Register<EarthquakeInformationUpdated>(this, async (_, x) =>
			{
				if (!Mres.IsSet)
					await Task.Run(() => Mres.Wait());
				Mres.Reset();
				try
				{
					await Dispatcher.UIThread.InvokeAsync(() => SelectedSeries = EarthquakeSeries);
					var captureTask = CaptureImageAsync();
					var imageUrlSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
					await Task.WhenAll(
						MisskeyUploader.UploadEarthquakeInformation(x, captureTask, imageUrlSource),
						SlackUploader?.UploadEarthquakeInformation(x, imageUrlSource) ?? Task.CompletedTask
					).WaitAsync(TimeSpan.FromSeconds(10));
				}
				catch (TimeoutException)
				{
					Logger.LogWarning("地震情報の投稿が10秒以内に完了しませんでした。次のイベント受付を再開します。");
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

			StrongReferenceMessenger.Default.Register<TsunamiInformationUpdated>(this, async (_, x) =>
			{
				if (!Mres.IsSet)
					await Task.Run(() => Mres.Wait());

				Mres.Reset();
				try
				{
					await Dispatcher.UIThread.InvokeAsync(() => SelectedSeries = TsunamiSeries);
					var captureTask = CaptureImageAsync();
					var imageUrlSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
					await Task.WhenAll(
						MisskeyUploader.UploadTsunamiInformation(x, captureTask, imageUrlSource),
						SlackUploader?.UploadTsunamiInformation(x, imageUrlSource) ?? Task.CompletedTask
					).WaitAsync(TimeSpan.FromSeconds(10));
				}
				catch (TimeoutException)
				{
					Logger.LogWarning("津波情報の投稿が10秒以内に完了しませんでした。次のイベント受付を再開します。");
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

			ServiceLocator.Current.RequireService<TelegramProvideService>().StartAsync().ConfigureAwait(false);

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

		private void UpdateMiniMapLayers()
		{
			var layers = new List<MapLayer>();
			if (BackgroundMapLayers != null)
				layers.AddRange(BackgroundMapLayers);
			layers.Add(MiniMapLandLayer);
			if (BaseMapLayers != null)
				layers.AddRange(BaseMapLayers);
			layers.Add(MiniMapLandBorderLayer);
			if (OverlayMapLayers != null)
				layers.AddRange(OverlayMapLayers);
			MiniMap.Layers = layers.ToArray();
		}

		private void ResetMiniMapPosition()
		{
			if (!MiniMap.IsVisible)
				return;
			MiniMap.Navigate(new RectD(new PointD(22.289, 121.207), new PointD(31.128, 132.100)), TimeSpan.Zero, true);
		}

		protected override void OnClosed(EventArgs e)
		{
			KyoshinMonitorSeries?.Dispose();
			EarthquakeSeries?.Dispose();
			TsunamiSeries?.Dispose();
			base.OnClosed(e);
		}

		private IDisposable? MapDisplayParameterListener { get; set; }
		private IDisposable? MapNavigationRequestListener { get; set; }

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
						MapDisplayParameterListener = _selectedSeries.ObservePropertyChanged(x => x.MapDisplayParameter).Subscribe(x =>
						{
							Dispatcher.UIThread.Post(() => Map.Padding = x.Padding);
							LandLayer.CustomColorMap = x.CustomColorMap;
							MiniMapLandLayer.CustomColorMap = x.CustomColorMap;
							UpdateMapLayers();
							UpdateMiniMapLayers();
						});
						Map.Padding = _selectedSeries.MapDisplayParameter.Padding;
						LandLayer.CustomColorMap = _selectedSeries.MapDisplayParameter.CustomColorMap;
						MiniMapLandLayer.CustomColorMap = _selectedSeries.MapDisplayParameter.CustomColorMap;

						MapNavigationRequestListener = _selectedSeries.ObservePropertyChanged(x => x.MapNavigationRequest).Subscribe(OnMapNavigationRequested);
						OnMapNavigationRequested(_selectedSeries.MapNavigationRequest);

						UpdateMapLayers();
						UpdateMiniMapLayers();
					}
					SeriesContent.Content = _selectedSeries?.DisplayControl;
				}
			}
		}
		private void OnMapNavigationRequested(MapNavigationRequest? e)
			=> StrongReferenceMessenger.Default.Send(e ?? new MapNavigationRequest(null));


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
			await Task.Delay(100); // 画面更新待ち
			await DrawingContextHelper.RenderAsync(Canvas, this, Bounds, SkiaPlatform.DefaultDpi);
			var render = sw.Elapsed;

			using var stream = new MemoryStream();
			using (var data = Bitmap.Encode(SKEncodedImageFormat.Webp, 100))
				data.SaveTo(stream);
			var save = sw.Elapsed;

			Logger.LogInformation("Total: {TotalMilliseconds}ms Measure: {TotalMilliseconds2}ms Arrange: {TotalMilliseconds3}ms Render: {TotalMilliseconds4}ms Save: {TotalMilliseconds5}ms", save.TotalMilliseconds, measure.TotalMilliseconds, (arrange - measure).TotalMilliseconds, (render - arrange - measure).TotalMilliseconds, (save - render - arrange - measure).TotalMilliseconds);
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
			await DrawingContextHelper.RenderAsync(Canvas, this, Bounds, SkiaPlatform.DefaultDpi);
			var render = sw.Elapsed;

			using (var data = Bitmap.Encode(SKEncodedImageFormat.Webp, 100))
				await Task.Run(() => data.SaveTo(outputStream));
			var save = sw.Elapsed;

			Logger.LogInformation("Total: {TotalMilliseconds}ms Measure: {TotalMilliseconds2}ms Arrange: {TotalMilliseconds3}ms Render: {TotalMilliseconds4}ms Save: {TotalMilliseconds5}ms", save.TotalMilliseconds, measure.TotalMilliseconds, (arrange - measure).TotalMilliseconds, (render - arrange - measure).TotalMilliseconds, (save - render - arrange - measure).TotalMilliseconds);
		}
	}

	public record CaptureResult(byte[] Data, TimeSpan TotalTime, TimeSpan MeasureTime, TimeSpan ArrangeTime, TimeSpan RenderTime, TimeSpan SaveTime);
}
