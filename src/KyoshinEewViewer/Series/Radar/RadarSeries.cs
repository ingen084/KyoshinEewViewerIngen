using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Radar.Models;
using KyoshinEewViewer.Services;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Series.Radar;

public partial class RadarSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(RadarSeries), "radar", "雨雲(β)", new FAFontIconSource { Glyph = "\xf740", FontFamily = new FontFamily(Utils.IconFontName) }, false, "雨雲レーダー画像を表示します。(試験機能)");

	public HttpClient Client { get; }
	private ILogger Logger { get; }
	private RadarImagePuller Puller { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private InformationCacheService CacheService { get; }
	private TimerService TimerService { get; }

	[ObservableProperty]
	public partial DateTime CurrentDateTime { get; set; } = DateTime.Now;
	[ObservableProperty]
	public partial bool IsLoading { get; set; } = true;

	private int _timeSliderValue;
	public int TimeSliderValue
	{
		get => _timeSliderValue;
		set {
			if (_timeSliderValue == value)
				return;
			SetProperty(ref _timeSliderValue, value);
			UpdateTiles().ConfigureAwait(false);
		}
	}
	[ObservableProperty]
	public partial int TimeSliderSize { get; set; } = 1;

	[ObservableProperty]
	public partial JmaRadarTime[]? JmaRadarTimes { get; set; }

	public RadarNodataBorderLayer BorderLayer { get; set; }

	/// <summary>
	/// 自動で取得し直すまでの最短の間隔
	/// </summary>
	private static readonly TimeSpan MinAutoReloadInterval = TimeSpan.FromSeconds(50);
	private long _lastReloadTick;
	private int _latestRealTimeIndex;
	private string? _appliedNodataUrl;

	private bool IsAutoReloadDue => Environment.TickCount64 - Interlocked.Read(ref _lastReloadTick) >= MinAutoReloadInterval.TotalMilliseconds;

	public RadarSeries(ILogger<RadarSeries> logger, KyoshinEewViewerConfiguration config, InformationCacheService cacheService, TimerService timerService) : base(MetaData)
	{
		Logger = logger;
		Config = config;
		TimerService = timerService;
		CacheService = cacheService;
		Client = new HttpClient(new HttpClientHandler()
		{
			AutomaticDecompression = DecompressionMethods.All
		});
		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Utils.Version};twitter@ingen084");
		Puller = new RadarImagePuller(AppLog.Create<RadarImagePuller>(), Client, CacheService);

		BorderLayer = new RadarNodataBorderLayer();
		MapDisplayParameter = new()
		{
			BorderEmphasis = true,
			Padding = new(0, 50, 0, 0),
			OverlayLayers = [BorderLayer],
			LayerSets = [
				new(10, LandLayerType.MunicipalityWeatherWarningArea),
				new(0, LandLayerType.EarthquakeInformationPrefecture),
			],
		};
	}

	private RadarView? _control;
	public override Control DisplayControl => _control ?? throw new Exception("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [
		new BasicSettingPage<RadarPage>("\xf740", "雨雲レーダー", []),
	];

	public override void Initialize()
	{
		TimerService.TimerElapsed += async t =>
		{
			// 表示していない間は取得しない
			if (t.Second != 20 || !IsDisplayed)
				return;
			// 自動更新が有効であれば更新を そうでなければキャッシュの揮発を行う
			if (!Config.Radar.AutoUpdate)
				await UpdateTiles();
			else if (IsAutoReloadDue)
				await Reload(false);
		};
		TimerService.StartMainTimer();

		this.ObservePropertyChanged(x => x.IsDisplayed, pushCurrentValueOnSubscribe: false).SubscribeLoggingErrors(displayed =>
		{
			if (displayed)
				OnDisplayed().ConfigureAwait(false);
			else
				ReleaseTiles();
		});
	}

	private Task OnDisplayed()
	{
		// 一度も取得できていなければ取得する。取得済みの場合は、自動更新が有効で前回の取得から間隔が空いているときだけ取得し直す
		if (JmaRadarTimes == null || (Config.Radar.AutoUpdate && IsAutoReloadDue))
			return Reload(false);
		return UpdateTiles();
	}

	/// <summary>
	/// 表示していない間はタイルを保持しない
	/// </summary>
	private void ReleaseTiles()
	{
		Puller.Cleanup();
		var oldLayer = MapDisplayParameter.BaseLayers?.FirstOrDefault() as ImageTileLayer;
		MapDisplayParameter = MapDisplayParameter with { BaseLayers = null };
		oldLayer?.Provider.Dispose();
	}

	public override Size MinViewSize { get; } = new(400, 500);

	public override void RecreateDisplayControl()
	{
		_control = new RadarView { DataContext = this };
		// 取得は表示されたときに行う。取得済みであれば最新の実況に戻す
		if (JmaRadarTimes != null)
			TimeSliderValue = _latestRealTimeIndex;
	}

	public Task ReloadFromGui() => Reload(false);

	public async Task Reload(bool init)
	{
		if (Client == null)
			return;
		// 一度も取得できていない場合は、最新の実況を選択する
		init |= JmaRadarTimes == null;
		Interlocked.Exchange(ref _lastReloadTick, Environment.TickCount64);
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
			var rbt = await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await response.Content.ReadAsStreamAsync()) ?? throw new Exception("データが取得できませんでした");
			var fbt = await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await response2.Content.ReadAsStreamAsync()) ?? throw new Exception("データが取得できませんでした");
			var realBaseTimes = rbt.OrderBy(j => j.BaseDateTime);
			var futureBaseTimes = fbt.OrderBy(j => j.ValidDateTime);
			JmaRadarTimes = realBaseTimes.Concat(futureBaseTimes).ToArray();
			TimeSliderSize = JmaRadarTimes?.Length - 1 ?? 0;
			_latestRealTimeIndex = realBaseTimes.Count() - 1;
			if (init && _timeSliderValue != _latestRealTimeIndex)
				TimeSliderValue = _latestRealTimeIndex;
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
			if (!IsDisplayed || JmaRadarTimes == null || JmaRadarTimes.Length <= _timeSliderValue || Client == null)
				return;

			Puller.Cleanup();

			var val = JmaRadarTimes[_timeSliderValue];
			if (val is null)
				return;
			CurrentDateTime = val.ValidDateTime?.AddHours(9) ?? throw new Exception("ValidTime が取得できません");
			var oldLayer = MapDisplayParameter.BaseLayers?.FirstOrDefault() as ImageTileLayer;
			var baseDateTime = val.BaseDateTime ?? throw new Exception("BaseTime が取得できません");
			var validDateTime = val.ValidDateTime ?? throw new Exception("ValidTime が取得できません");
			MapDisplayParameter = MapDisplayParameter with { BaseLayers = [new ImageTileLayer(new RadarImageTileProvider(Puller, CacheService, baseDateTime, validDateTime))] };
			oldLayer?.Provider.Dispose();

			var url = $"https://www.jma.go.jp/bosai/jmatile/data/nowc/{baseDateTime:yyyyMMddHHmm00}/none/{validDateTime:yyyyMMddHHmm00}/surf/hrpns_nd/data.geojson?id=hrpns_nd";
			// 同じデータを読み直さない
			if (url == _appliedNodataUrl)
				return;
			var geoJson = await JsonSerializer.DeserializeAsync<GeoJson>(await CacheService.TryGetOrFetchImageAsStreamAsync(url, async () =>
			{
				var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
				if (!response.IsSuccessStatusCode)
					throw new Exception("ステータスコード異常 status: " + response.StatusCode);
				return (await response.Content.ReadAsStreamAsync(), DateTime.Now.AddHours(3));
			}));

			if (geoJson != null)
			{
				BorderLayer.UpdatePoints(geoJson);
				_appliedNodataUrl = url;
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "nodata範囲の取得に失敗");
		}
	}


	public override void Dispose()
	{
		Puller.Shutdown();
		if (MapDisplayParameter.BaseLayers?.FirstOrDefault() is ImageTileLayer l)
			l.Provider.Dispose();
		GC.SuppressFinalize(this);
	}
}
