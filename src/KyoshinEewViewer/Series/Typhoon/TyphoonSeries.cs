using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Series.Typhoon.Services;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Typhoon;

internal class TyphoonSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(TyphoonSeries), "typhoon", "台風情報α", "\xf751", false, "台風の実況･予報円を表示します。");

	private ILogger Logger { get; }
	private TyphoonWatchService TyphoonWatchService { get; set; }


	public TyphoonSeries(ILogManager logManager, TelegramProvideService telegramProvider, TimerService timer) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<TyphoonSeries>();

		Logger = logManager.GetLogger<TyphoonSeries>();
		TyphoonWatchService = new(logManager, telegramProvider, timer);

		MapDisplayParameter = new() {
			Padding = new(230, 0, 0, 0),
			OverlayLayers = [TyphoonLayer],
			LayerSets = [
				new(0, LandLayerType.EarthquakeInformationPrefecture),
			],
		};

		if (Design.IsDesignMode)
		{
			Typhoons = [
			new(
				"",
				"台風0号",
				false,
				new(
					"大型",
					"猛烈な",
					DateTime.Now,
					"現況",
					"なんちゃらの南約3km",
					1000,
					55,
					true,
					75,
					null!,
					null!,
					null
				),
				null)
			];
			SelectedTyphoon = Typhoons.First();
			return;
		}

		// 台風情報更新時
		TyphoonWatchService.TyphoonUpdated += t =>
		{
			if (!Enabled)
				return;
			Typhoons = TyphoonWatchService.Typhoons.ToArray();
			SelectedTyphoon = t;
		};

		this.WhenAnyValue(x => x.SelectedTyphoon).Subscribe(i =>
		{
			if (i == null)
			{
				TyphoonLayer.TyphoonItems = [];
				MapNavigationRequest = null;
				return;
			}

			var zoomPoints = new List<Location>(PathGenerator.GetCircleRect(i.Current.Center, i.Current.Strong is null ? 2000000 : (i.Current.Strong.RangeKilometer * 1000 * 1.1)));

			if (i.ForecastPlaces is { } forecastPlaces)
				foreach (var f in forecastPlaces)
					zoomPoints.AddRange(PathGenerator.GetCircleRect(f.Center, f.Strong is null ? 100 : f.Strong.RangeKilometer * 1000 * 1.1));

			if (zoomPoints.Count != 0)
				MapNavigationRequest = new(zoomPoints.CalcRect());
			TyphoonLayer.TyphoonItems = [i];
		});

		TyphoonWatchService.WhenAnyValue(x => x.Enabled).Subscribe(e =>
		{
			Enabled = e;
			if (e)
			{
				Typhoons = TyphoonWatchService.Typhoons.ToArray();
				SelectedTyphoon = Typhoons.LastOrDefault();
			}
			else
			{
				Typhoons = null;
				SelectedTyphoon = null;
			}
		});
	}

	private bool _enable;
	public bool Enabled
	{
		get => _enable;
		private set => this.RaiseAndSetIfChanged(ref _enable, value);
	}

	private TyphoonView? _control;
	public override Control DisplayControl => _control ?? throw new Exception();
	public override ISettingPage[] SettingPages => [];

	private TyphoonItem[]? _typhoons;
	public TyphoonItem[]? Typhoons
	{
		get => _typhoons;
		set => this.RaiseAndSetIfChanged(ref _typhoons, value);
	}

	private TyphoonItem? _selectedTyphoon;
	public TyphoonItem? SelectedTyphoon
	{
		get => _selectedTyphoon;
		set => this.RaiseAndSetIfChanged(ref _selectedTyphoon, value);
	}

	private TyphoonLayer TyphoonLayer { get; } = new();

	public override Size MinViewSize { get; } = new(400, 550);

	public override void RecreateDisplayControl()
		=> _control = new TyphoonView { DataContext = this };

	public async Task OpenXml()
	{
		if (TopLevel.GetTopLevel(_control) is not { } topLevel)
			return;

		try
		{
			var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
			{
				Title = "任意のXML電文を開く",
				FileTypeFilter = new List<FilePickerFileType>()
				{
					FilePickerFileTypes.All,
				},
				AllowMultiple = false,
			});
			if (files is not { Count: > 0 } || !files[0].Name.EndsWith(".xml"))
				return;

			await using var stream = await files[0].OpenReadAsync();
			var tc = TyphoonWatchService.ProcessXml(stream, files[0].Name);
			Typhoons = tc != null ? [tc] : null;
			SelectedTyphoon = tc;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "外部XMLの読み込みに失敗しました");
		}
	}
}
