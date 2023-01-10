using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Tsunami;
public class TsunamiSeries : SeriesBase
{
	public TelegramProvideService TelegramProvider { get; }
	public NotificationService NotificationService { get; }
	public TsunamiLayer TsunamiLayer { get; }
	public Microsoft.Extensions.Logging.ILogger Logger { get; }

	public TsunamiSeries() : base("津波情報", new FontIcon { Glyph = "\xe515", FontFamily = new("IconFont") })
	{
		TelegramProvider = Locator.Current.GetService<TelegramProvideService>() ?? throw new Exception("TelegramProvideService の解決に失敗しました");
		NotificationService = Locator.Current.GetService<NotificationService>() ?? throw new Exception("NotificationService の解決に失敗しました");
		Logger = LoggingService.CreateLogger(this);

		TsunamiLayer = new TsunamiLayer();
		MessageBus.Current.Listen<MapLoaded>().Subscribe(e => TsunamiLayer.Map = e.Data);
		BackgroundMapLayers = new[] { TsunamiLayer };

		if (Design.IsDesignMode)
		{
			SourceName = "Source";
			Current = new TsunamiInfo()
			{
				SpecialState = "テスト",
				ReportedAt = DateTime.Now,
				ExpireAt = DateTime.Now.AddHours(2),
				MajorWarningAreas = new TsunamiWarningArea[]
				{
					new(0, "地域A", "10m超", "ただちに津波襲来", DateTime.Now),
					new(0, "地域B", "10m超", "第1波到達を確認", DateTime.Now),
					new(0, "地域C", "10m", "14:30 到達見込み", DateTime.Now),
					new(0, "地域D", "巨大", "14:45", DateTime.Now),
					new(0, "あまりにも長すぎる名前の地域", "ながい", "あまりにも長すぎる説明", DateTime.Now),
				},
				WarningAreas = new TsunamiWarningArea[]
				{
					new(0, "地域E", "高い", "14:30", DateTime.Now),
				},
				AdvisoryAreas = new TsunamiWarningArea[]
				{
					new(0, "地域F", "1m", "14:30", DateTime.Now),
					new(0, "地域G", "", "14:30", DateTime.Now),
				},
				ForecastAreas = new TsunamiWarningArea[]
				{
					new(0, "地域H", "0.2m未満", "", DateTime.Now),
				},
			};
			return;
		}

		TelegramProvider.Subscribe(
			InformationCategory.Tsunami,
			async (s, t) =>
			{
				SourceName = s;
				var lt = t.LastOrDefault(t => t.Title == "津波警報・注意報・予報a");
				if (lt == null)
					return;
				using var stream = await lt.GetBodyAsync();
				using var report = new JmaXmlDocument(stream);
				var tsunami = ProcessInformation(report);
				if (tsunami == null || tsunami.ExpireAt <= TimerService.Default.CurrentTime)
					return;
				Current = tsunami;
			},
			async t =>
			{
				if (t.Title != "津波警報・注意報・予報a")
					return;
				using var stream = await t.GetBodyAsync();
				using var report = new JmaXmlDocument(stream);
				var tsunami = ProcessInformation(report);
				if (tsunami == null || (Current != null && tsunami.ReportedAt <= Current.ReportedAt) || tsunami.ExpireAt <= TimerService.Default.CurrentTime)
					return;
				Current = tsunami;
			},
			s =>
			{
				SourceName = null;
				// TODO: 状態管理はもうちょっとちゃんとやる必要がある
			}
		);
	}

	private TsunamiView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	private string? _sourceName;
	/// <summary>
	/// 情報の受信元
	/// </summary>
	public string? SourceName
	{
		get => _sourceName;
		set => this.RaiseAndSetIfChanged(ref _sourceName, value);
	}

	private bool _isFault;
	public bool IsFault
	{
		get => _isFault;
		set => this.RaiseAndSetIfChanged(ref _isFault, value);
	}

	public bool IsDebugBuiid =>
#if DEBUG
		true;
#else
		false;
#endif

	private TsunamiInfo? _current;
	public TsunamiInfo? Current
	{
		get => _current;
		set {
			this.RaiseAndSetIfChanged(ref _current, value);
			TsunamiLayer.Current = value;
			if (_current == null)
				MapPadding = new Avalonia.Thickness(0);
			else
				MapPadding = new Avalonia.Thickness(360, 0, 0, 0);
		}
	}

	public override void Activating()
	{
		if (control != null)
			return;
		control = new TsunamiView
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

			using var stream = File.OpenRead(file);
			using var report = new JmaXmlDocument(stream);
			Current = ProcessInformation(report);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "外部XMLの読み込みに失敗しました");
		}
	}

	public TsunamiInfo? ProcessInformation(JmaXmlDocument report)
	{
		if (report.Control.Title != "津波警報・注意報・予報a")
			return null;
		var tsunami = new TsunamiInfo();
		if (report.Control.Status != "通常")
			tsunami.SpecialState = report.Control.Status;
		tsunami.ReportedAt = report.Head.ReportDateTime.DateTime;
		tsunami.ExpireAt = report.Head.ValidDateTime?.DateTime;

		var forecastAreas = new List<TsunamiWarningArea>();
		var advisoryAreas = new List<TsunamiWarningArea>();
		var warningAreas = new List<TsunamiWarningArea>();
		var majorWarningAreas = new List<TsunamiWarningArea>();
		var tsunamiForecast = report.TsunamiBody.Tsunami?.Forecast ?? throw new Exception("Body/Tsunami/Forecast がみつかりません");
		foreach (var i in tsunamiForecast.Items)
		{
			if (i.Category.Kind.Code
				is "00" // 津波なし
				or "50" // 警報解除
				or "60" // 注意報解除
			)
				continue;

			var height = i.MaxHeight?.TsunamiHeight.Description;
			// 津波予報時かつ高さの情報がない場合は海面変動の文言を入れる
			if (height == null && i.Category.Kind.Code is "71" or "72" or "73")
				height = "若干の海面変動";
			var area = new TsunamiWarningArea(
				i.Area.Code,
				i.Area.Name,
				Utils.ConvertToShortWidthString(height ?? throw new Exception("TsunamiHeight/Description がみつかりません")),
				Utils.ConvertToShortWidthString(i.FirstHeight?.ArrivalTime?.ToString("HH:mm 到達見込み") ?? i.FirstHeight?.Condition ?? ""), // 予報の場合は要素が存在しないので空文字に
				i.FirstHeight?.ArrivalTime?.DateTime ?? // 到達時刻はソートに使用するため Condition に応じて暫定値を入れる
				(i.FirstHeight?.Condition == "ただちに津波来襲と予測" ? tsunami.ReportedAt.AddHours(-3) : (DateTime?)null) ??
				(i.FirstHeight?.Condition == "津波到達中と推測" ? tsunami.ReportedAt.AddHours(-2) : (DateTime?)null) ??
				(i.FirstHeight?.Condition == "第１波の到達を確認" ? tsunami.ReportedAt.AddHours(-1) : (DateTime?)null) ??
				tsunami.ReportedAt // 算出できなければ電文の作成時刻を入れる
			);

			if (i.Category.Kind.Code is "51") // 警報
				warningAreas.Add(area);
			else if (i.Category.Kind.Code is "52" or "53") // 大津波警報
				majorWarningAreas.Add(area);
			else if (i.Category.Kind.Code is "62") // 注意報
				advisoryAreas.Add(area);
			else if (i.Category.Kind.Code is "71" or "72" or "73") // 予報
				forecastAreas.Add(area);
		}
		if (forecastAreas.Count > 0)
			tsunami.ForecastAreas = forecastAreas.OrderBy(a => a.ArrivalTime).ToArray();
		if (advisoryAreas.Count > 0)
			tsunami.AdvisoryAreas = advisoryAreas.OrderBy(a => a.ArrivalTime).ToArray();
		if (warningAreas.Count > 0)
			tsunami.WarningAreas = warningAreas.OrderBy(a => a.ArrivalTime).ToArray();
		if (majorWarningAreas.Count > 0)
			tsunami.MajorWarningAreas = majorWarningAreas.OrderBy(a => a.ArrivalTime).ToArray();

		return tsunami;
	}
}
