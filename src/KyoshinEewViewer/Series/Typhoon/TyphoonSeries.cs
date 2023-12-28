using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Series.Typhoon.Services;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Typhoon;

internal class TyphoonSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(TyphoonSeries), "typhoon", "台風情報α", new FontIconSource { Glyph = "\xf751", FontFamily = new(Utils.IconFontName) }, false, "台風の実況･予報円を表示します。");

	private ILogger Logger { get; }
	private TyphoonWatchService TyphoonWatchService { get; set; }


	public TyphoonSeries(ILogManager logManager, TelegramProvideService telegramProvider, TimerService timer) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<TyphoonSeries>();

		Logger = logManager.GetLogger<TyphoonSeries>();
		TyphoonWatchService = new(logManager, telegramProvider, timer);
		MapPadding = new(230, 0, 0, 0);
		OverlayLayers = new[] { TyphoonLayer };
		LayerSets = [
			new(0, LandLayerType.EarthquakeInformationPrefecture),
		];

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
				FocusBound = null;
				return;
			}

			var zoomPoints = new List<Location>
			{
				new(i.Current.Center.Latitude - 2.5f, i.Current.Center.Longitude - 5),
				new(i.Current.Center.Latitude + 2.5f, i.Current.Center.Longitude + 5)
			};

			if (i.ForecastPlaces is { } forecastPlaces)
				foreach (var c in forecastPlaces.Select(f => f.Center))
				{
					zoomPoints.Add(new(c.Latitude - 2.5f, c.Longitude - 5));
					zoomPoints.Add(new(c.Latitude + 2.5f, c.Longitude + 5));
				}

			if (zoomPoints.Count != 0)
			{
				// 自動ズーム範囲を計算
				var minLat = float.MaxValue;
				var maxLat = float.MinValue;
				var minLng = float.MaxValue;
				var maxLng = float.MinValue;
				foreach (var p in zoomPoints)
				{
					if (minLat > p.Latitude)
						minLat = p.Latitude;
					if (minLng > p.Longitude)
						minLng = p.Longitude;

					if (maxLat < p.Latitude)
						maxLat = p.Latitude;
					if (maxLng < p.Longitude)
						maxLng = p.Longitude;
				}
				var rect = new Avalonia.Rect(minLat, minLng, maxLat - minLat, maxLng - minLng);

				FocusBound = rect;
			}
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

	public override void Activating()
	{
		if (_control != null)
			return;
		_control = new TyphoonView
		{
			DataContext = this,
		};
	}
	public override void Deactivated() { }

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

			using var stream = await files[0].OpenReadAsync();
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
