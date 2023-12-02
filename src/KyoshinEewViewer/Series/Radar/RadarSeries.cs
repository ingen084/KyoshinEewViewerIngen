using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Radar.Models;
using KyoshinEewViewer.Services;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Radar;

public class RadarSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(RadarSeries), "radar", "雨雲(β)", new FontIconSource { Glyph = "\xf740", FontFamily = new FontFamily(Utils.IconFontName) }, false, "雨雲レーダー画像を表示します。(試験機能)");

	public HttpClient Client { get; }
	private ILogger Logger { get; }
	private RadarImagePuller Puller { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private InformationCacheService CacheService { get; }
	private TimerService TimerService { get; }

	private DateTime _currentDateTime = DateTime.Now;
	public DateTime CurrentDateTime
	{
		get => _currentDateTime;
		set => this.RaiseAndSetIfChanged(ref _currentDateTime, value);
	}
	private bool _isLoading = true;
	public bool IsLoading
	{
		get => _isLoading;
		set => this.RaiseAndSetIfChanged(ref _isLoading, value);
	}

	private int _timeSliderValue;
	public int TimeSliderValue
	{
		get => _timeSliderValue;
		set {
			if (_timeSliderValue == value)
				return;
			this.RaiseAndSetIfChanged(ref _timeSliderValue, value);
			UpdateTiles().ConfigureAwait(false);
		}
	}
	private int _timeSliderSize = 1;
	public int TimeSliderSize
	{
		get => _timeSliderSize;
		set => this.RaiseAndSetIfChanged(ref _timeSliderSize, value);
	}

	private JmaRadarTime[]? _jmaRadarTimes;
	public JmaRadarTime[]? JmaRadarTimes
	{
		get => _jmaRadarTimes;
		set => this.RaiseAndSetIfChanged(ref _jmaRadarTimes, value);
	}

	public RadarNodataBorderLayer BorderLayer { get; set; }

	public RadarSeries(ILogManager logManager, KyoshinEewViewerConfiguration config, InformationCacheService cacheService, TimerService timerService) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<RadarSeries>();

		Logger = logManager.GetLogger<RadarSeries>();
		Config = config;
		TimerService = timerService;
		CacheService = cacheService;
		MapPadding = new Avalonia.Thickness(0, 50, 0, 0);
		Client = new HttpClient(new HttpClientHandler()
		{
			AutomaticDecompression = DecompressionMethods.All
		});
		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Assembly.GetExecutingAssembly().GetName().Version};twitter@ingen084");
		Puller = new RadarImagePuller(logManager, Client, CacheService);

		BorderLayer = new RadarNodataBorderLayer();
		OverlayLayers = new[] { BorderLayer };
	}

	private RadarView? _control;
	public override Control DisplayControl => _control ?? throw new Exception("初期化前にコントロールが呼ばれています");

	public override void Activating()
	{
		if (_control != null)
			return;
		_control = new RadarView
		{
			DataContext = this,
		};
		Reload(true).ConfigureAwait(false);
		TimerService.TimerElapsed += async t =>
		{
			if (t.Second != 20)
				return;
			// 自動更新が有効であれば更新を そうでなければキャッシュの揮発を行う
			if (Config.Radar.AutoUpdate)
				await Reload(false);
			else
				await UpdateTiles();
		};
		TimerService.StartMainTimer();
	}
	public async Task Reload(bool init = false)
	{
		if (Client == null)
			return;
		IsLoading = true;

		try
		{
			using var response = await Client.GetAsync("https://www.jma.go.jp/bosai/jmatile/data/nowc/targetTimes_N1.json");
			using var response2 = await Client.GetAsync("https://www.jma.go.jp/bosai/jmatile/data/nowc/targetTimes_N2.json");
			if (!response.IsSuccessStatusCode || !response2.IsSuccessStatusCode)
			{
				IsLoading = false;
				return;
			}
			var realBaseTimes = (await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await response.Content.ReadAsStreamAsync()))?.OrderBy(j => j.BaseDateTime);
			var futureBaseTimes = (await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await response2.Content.ReadAsStreamAsync()))?.OrderBy(j => j.ValidDateTime);
			if (realBaseTimes is null || futureBaseTimes is null)
				throw new Exception("データが取得できませんでした");
			var baseTimes = realBaseTimes.Concat(futureBaseTimes).ToArray();
			JmaRadarTimes = baseTimes;
			TimeSliderSize = JmaRadarTimes?.Length - 1 ?? 0;
			if (init)
				TimeSliderValue = realBaseTimes?.Count() - 1 ?? TimeSliderSize;
			else
				await UpdateTiles();
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "レーダー更新中にエラー");
		}
		IsLoading = false;
	}
	public async Task UpdateTiles()
	{
		try
		{
			if (JmaRadarTimes == null || JmaRadarTimes.Length <= _timeSliderValue || Client == null)
				return;

			Puller.Cleanup();

			var val = JmaRadarTimes[_timeSliderValue];
			if (val is null)
				return;
			CurrentDateTime = val.ValidDateTime?.AddHours(9) ?? throw new Exception("ValidTime が取得できません");
			var oldLayer = BaseLayers?.FirstOrDefault() as ImageTileLayer;
			var baseDateTime = val.BaseDateTime ?? throw new Exception("BaseTime が取得できません");
			var validDateTime = val.ValidDateTime ?? throw new Exception("ValidTime が取得できません");
			BaseLayers = new[] { new ImageTileLayer(new RadarImageTileProvider(Puller, CacheService, baseDateTime, validDateTime)) };
			oldLayer?.Provider.Dispose();

			var url = $"https://www.jma.go.jp/bosai/jmatile/data/nowc/{baseDateTime:yyyyMMddHHmm00}/none/{validDateTime:yyyyMMddHHmm00}/surf/hrpns_nd/data.geojson?id=hrpns_nd";
			var geoJson = await JsonSerializer.DeserializeAsync<GeoJson>(await CacheService.TryGetOrFetchImageAsStreamAsync(url, async () =>
			{
				var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
				if (!response.IsSuccessStatusCode)
					throw new Exception("ステータスコード異常 status: " + response.StatusCode);
				return (await response.Content.ReadAsStreamAsync(), DateTime.Now.AddHours(3));
			}));

			if (geoJson != null)
				BorderLayer.UpdatePoints(geoJson);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "nodata範囲の取得に失敗");
		}
	}

	public override void Deactivated() { }

	public override void Dispose()
	{
		Puller.Shutdown();
		if (BaseLayers?.FirstOrDefault() is ImageTileLayer l)
			l.Provider.Dispose();
		GC.SuppressFinalize(this);
	}
}
