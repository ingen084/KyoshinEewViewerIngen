using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Series.Typhoon.Services;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
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
	public TyphoonSeries() : this(null) { }
	public TyphoonSeries(TelegramProvideService? telegramProvideService) : base("台風情報α", new FontIcon { Glyph = "\xf751", FontFamily = new("IconFont") })
	{
		Logger = LoggingService.CreateLogger(this);
		TyphoonWatchService = new(telegramProvideService ?? Locator.Current.GetService<TelegramProvideService>() ?? throw new Exception("TelegramProvideService の解決に失敗しました"));

		MapPadding = new(230, 0, 0, 0);

		if (Design.IsDesignMode)
		{
			Typhoons = new TyphoonItem[]
			{
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
			};
			SelectedTyphoon = Typhoons.First();
			return;
		}
		OverlayLayers = new[] { TyphoonLayer };

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
				TyphoonLayer.TyphoonItems = Array.Empty<TyphoonItem>();
				FocusBound = null;
				return;
			}

			var zoomPoints = new List<Location>
			{
				new(i.Current.Center.Latitude - 2.5f, i.Current.Center.Longitude - 5),
				new(i.Current.Center.Latitude + 2.5f, i.Current.Center.Longitude + 5)
			};

			if (i.ForecastPlaces is TyphoonPlace[] forecastPlaces)
				foreach (var c in forecastPlaces.Select(f => f.Center))
				{
					zoomPoints.Add(new(c.Latitude - 2.5f, c.Longitude - 5));
					zoomPoints.Add(new(c.Latitude + 2.5f, c.Longitude + 5));
				}

			if (zoomPoints.Any())
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
			TyphoonLayer.TyphoonItems = new[] { i };
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

	private Microsoft.Extensions.Logging.ILogger Logger { get; }
	private TyphoonWatchService TyphoonWatchService { get; }

	private bool _enable;
	public bool Enabled
	{
		get => _enable;
		private set => this.RaiseAndSetIfChanged(ref _enable, value);
	}

	private TyphoonView? control;
	public override Control DisplayControl => control ?? throw new Exception();

	private TyphoonItem[]? typhoons;
	public TyphoonItem[]? Typhoons
	{
		get => typhoons;
		set => this.RaiseAndSetIfChanged(ref typhoons, value);
	}

	private TyphoonItem? selectedTyphoon;
	public TyphoonItem? SelectedTyphoon
	{
		get => selectedTyphoon;
		set => this.RaiseAndSetIfChanged(ref selectedTyphoon, value);
	}

	private TyphoonLayer TyphoonLayer { get; } = new();

	public override void Activating()
	{
		if (control != null)
			return;
		control = new TyphoonView
		{
			DataContext = this,
		};
	}
	public override void Deactivated() { }

	public async Task OpenXML()
	{
		if (App.MainWindow == null)
			return;

		try
		{
			var ofd = new OpenFileDialog();
			ofd.Filters.Add(new FileDialogFilter
			{
				Name = "防災情報XML",
				Extensions = new List<string>
				{
					"xml"
				},
			});
			ofd.AllowMultiple = false;
			var files = await ofd.ShowAsync(App.MainWindow);
			var file = files?.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(file))
				return;
			if (!File.Exists(file))
				return;

			var tc = TyphoonWatchService.ProcessXml(File.OpenRead(file), file);
			TyphoonLayer.TyphoonItems = tc != null ? new[] { tc } : null;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "外部XMLの読み込みに失敗しました");
		}
	}
}
