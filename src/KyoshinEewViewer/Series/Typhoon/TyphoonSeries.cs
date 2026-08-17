using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Series.Typhoon.Services;
using KyoshinEewViewer.Services;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Series.Typhoon;

internal partial class TyphoonSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(TyphoonSeries), "typhoon", "台風情報α", new FAFontIconSource { Glyph = "\xf751", FontFamily = new(Utils.IconFontName) }, false, "台風の実況･予報円を表示します。");

	private ILogger Logger { get; }
	private TyphoonWatchService TyphoonWatchService { get; set; }


	public TyphoonSeries(ILogger<TyphoonSeries> logger, TelegramProvideService telegramProvider, TimerService timer) : base(MetaData)
	{
		Logger = logger;
		TyphoonWatchService = new(AppLog.Create<Services.TyphoonWatchService>(), telegramProvider, timer);

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
		TyphoonWatchService.TyphoonUpdated
			.ObserveOn(UiScheduler.Instance)
			.Subscribe(t =>
			{
				if (!Enabled)
					return;
				Typhoons = TyphoonWatchService.Typhoons.ToArray();
				SelectedTyphoon = t;
			});

		this.ObservePropertyChanged(x => x.SelectedTyphoon).Subscribe(i =>
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

		TyphoonWatchService.ObservePropertyChanged(x => x.Enabled).Subscribe(e =>
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

	[ObservableProperty]
	public partial bool Enabled { get; private set; }

	private TyphoonView? _control;
	public override Control DisplayControl => _control ?? throw new Exception();
	public override ISettingPage[] SettingPages => [];

	[ObservableProperty]
	public partial TyphoonItem[]? Typhoons { get; set; }

	[ObservableProperty]
	public partial TyphoonItem? SelectedTyphoon { get; set; }

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
