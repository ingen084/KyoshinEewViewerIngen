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

namespace KyoshinEewViewer.Series.Typhoon;

internal class TyphoonSeries : SeriesBase
{
	public TyphoonSeries() : this(null) { }
	public TyphoonSeries(TelegramProvideService? telegramProvideService) : base("台風情報α", new FontIcon { Glyph = "\xf751", FontFamily = new("IconFont") })
	{
		Logger = LoggingService.CreateLogger(this);
		TyphoonWatchService = new(telegramProvideService ?? Locator.Current.GetService<TelegramProvideService>() ?? throw new Exception("TelegramProvideService の解決に失敗しました"));

		if (Design.IsDesignMode)
		{
			TyphoonWatchService.Typhoons.Add(new("", "台風0号", false, new(
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
			), null));
			SelectedTyphoon = TyphoonWatchService.Typhoons.First();
			return;
		}
		OverlayLayers = new[] { TyphoonLayer };

		this.WhenAnyValue(x => x.SelectedTyphoon).Subscribe(i =>
		{
			if (i == null)
			{
				TyphoonLayer.TyphoonItems = Array.Empty<TyphoonItem>();
				return;
			}
			TyphoonLayer.TyphoonItems = new[] { i };
		});

		TyphoonWatchService.WhenAnyValue(x => x.Enabled).Subscribe(e =>
		{
			if (!e)
				return;
			this.RaisePropertyChanged(nameof(TyphoonWatchService));
			if (TyphoonWatchService.Typhoons.FirstOrDefault() is TyphoonItem fi)
				SelectedTyphoon = fi;
		});
	}

	private Microsoft.Extensions.Logging.ILogger Logger { get; }
	private TyphoonWatchService TyphoonWatchService { get; }

	private TyphoonView? control;
	public override Control DisplayControl => control ?? throw new Exception();

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

			var tc = TyphoonWatchService.ProcessXml(File.OpenRead(file));
			TyphoonLayer.TyphoonItems = tc != null ? new[] { tc } : null;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "外部XMLの読み込みに失敗しました");
		}
	}
}
