using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.Tsunami.Events;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Tsunami;
public class TsunamiSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(TsunamiSeries), "tsunami", "津波情報", new FontIconSource { Glyph = "\xe515", FontFamily = new(Utils.IconFontName) }, true, "津波情報を表示します。");

	private bool IsInitializing { get; set; }
	private ILogger Logger { get; set; }
	public KyoshinEewViewerConfiguration Config { get; }
	public TelegramProvideService TelegramProvider { get; }
	public NotificationService NotificationService { get; }
	public TsunamiLayer TsunamiLayer { get; }
	private MapData? MapData { get; set; }

	/// <summary>
	/// 期限切れの情報を揮発させるタイマー
	/// </summary>
	public Timer? ExpireTimer { get; set; }

	private SoundCategory SoundCategory { get; } = new("Tsunami", "津波情報");
	private Sound? NewSound { get; set; }
	private Sound? UpdatedSound { get; set; }
	private Sound? UpgradeSound { get; set; }
	private Sound? DowngradeSound { get; set; }

	public TsunamiSeries(ILogManager logManager, KyoshinEewViewerConfiguration config, TelegramProvideService telegramProvider, NotificationService notificationService, SoundPlayerService soundPlayer, TimerService timerService) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<TsunamiSeries>();

		Logger = logManager.GetLogger<TsunamiSeries>();
		Config = config;
		TelegramProvider = telegramProvider;
		NotificationService = notificationService;

		TsunamiLayer = new TsunamiLayer();
		MessageBus.Current.Listen<MapLoaded>().Subscribe(e => MapData = TsunamiLayer.Map = e.Data);
		BackgroundMapLayers = new[] { TsunamiLayer };

		NewSound = soundPlayer.RegisterSound(SoundCategory, "New", "津波情報の発表", "未発表状態から受信した際に鳴動します。\n{lv}: 警報種別 [fore, adv, warn, major]", new() { { "lv", "fore" }, });
		UpgradeSound = soundPlayer.RegisterSound(SoundCategory, "Upgrade", "警報/注意報の更新", "より上位の警報/注意報が発表された際に鳴動します。\n{lv}: 更新後の警報種別 [fore, adv, warn, major]", new() { { "lv", "warn" }, });
		DowngradeSound = soundPlayer.RegisterSound(SoundCategory, "Downgrade", "警報/注意報の解除", "最大の警報レベルが下がった時に鳴動します。\n{lv}: 解除後の警報種別 [none, fore, adv, warn, major]", new() { { "lv", "none" }, });
		UpdatedSound = soundPlayer.RegisterSound(SoundCategory, "Updated", "津波情報の更新", "他の津波関連の音声が再生されなかった場合、この音声が鳴動します。\n{lv}: 最大の警報種別 [fore, adv, warn, major]", new() { { "lv", "adv" }, });

		ExpireTimer = new Timer(_ =>
		{
			if (Current?.CheckExpired(timerService.CurrentTime) ?? false)
			{
				Current = null;
				FocusBound = null;
			}
		});

		TelegramProvider.Subscribe(
			InformationCategory.Tsunami,
			async (s, t) =>
			{
				IsInitializing = true;
				try
				{
					SourceName = s;
					var lt = t.LastOrDefault(t => t.Title == "津波警報・注意報・予報a");
					if (lt == null)
						return;
					using var stream = await lt.GetBodyAsync();
					using var report = new JmaXmlDocument(stream);
					(var tsunami, var bound) = ProcessInformation(report);
					if (tsunami == null || tsunami.ExpireAt <= timerService.CurrentTime)
						return;
					Current = tsunami;
					FocusBound = bound;
				}
				finally
				{
					IsInitializing = false;
				}
			},
			async t =>
			{
				if (t.Title != "津波警報・注意報・予報a")
					return;
				using var stream = await t.GetBodyAsync();
				using var report = new JmaXmlDocument(stream);
				(var tsunami, var bound) = ProcessInformation(report);
				if (tsunami == null || (Current != null && tsunami.ReportedAt <= Current.ReportedAt) || tsunami.CheckExpired(timerService.CurrentTime))
					return;
				Current = tsunami;
				FocusBound = bound;
			},
			s =>
			{
				SourceName = null;
				// TODO: 状態管理はもうちょっとちゃんとやる必要がある
			}
		);

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

		ExpireTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
	}

	private TsunamiView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

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
			// Series 自動切り替えのためのフラグ
			// 解除時以外の更新時にフラグが立つ
			var isUpdated = false;

			if (_current != value)
			{
				var level = value?.Level switch
				{
					TsunamiLevel.MajorWarning => "major",
					TsunamiLevel.Warning => "warn",
					TsunamiLevel.Advisory => "adv",
					TsunamiLevel.Forecast => "fore",
					_ => "none",
				};
				var levelStr = value?.Level switch
				{
					TsunamiLevel.MajorWarning => "大津波警報",
					TsunamiLevel.Warning => "津波警報",
					TsunamiLevel.Advisory => "津波注意報",
					TsunamiLevel.Forecast => "津波予報",
					_ => "",
				};

				// 発表
				if (
					(_current == null || _current.Level <= TsunamiLevel.None) && value != null &&
					(
						value.AdvisoryAreas != null ||
						value.ForecastAreas != null ||
						value.MajorWarningAreas != null ||
						value.WarningAreas != null
					)
				)
				{
					if (!NewSound?.Play(new Dictionary<string, string> { { "lv", level } }) ?? false)
						UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
					if (Config.Notification.Tsunami && levelStr != "")
						NotificationService?.Notify("津波情報", $"{levelStr}が発表されました。");
					isUpdated = true;
				}
				// 解除
				else if (_current != null && _current.Level > TsunamiLevel.None && (value == null || value.Level < _current.Level))
				{
					if (!DowngradeSound?.Play(new Dictionary<string, string> { { "lv", level } }) ?? false)
						UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
					if (Config.Notification.Tsunami)
						NotificationService?.Notify("津波情報", value?.Level switch
						{
							TsunamiLevel.MajorWarning => "大津波警報が引き続き発表されています。",
							TsunamiLevel.Warning => "大津波警報は津波警報に引き下げられました。",
							TsunamiLevel.Advisory => "津波警報は津波注意報に引き下げられました。",
							TsunamiLevel.Forecast => "津波警報・注意報は予報に引き下げられました。",
							_ => _current.Level == TsunamiLevel.Forecast ? "津波予報の情報期限が切れました。" : "津波警報・注意報・予報は解除されました。",
						});
					if (value != null)
						isUpdated = true;
				}
				// 引き上げ
				else if (_current != null && value != null && _current.Level < value.Level)
				{
					if (!UpgradeSound?.Play(new Dictionary<string, string> { { "lv", level } }) ?? false)
						UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
					if (Config.Notification.Tsunami)
						NotificationService?.Notify("津波情報", value.Level switch
						{
							TsunamiLevel.MajorWarning => "大津波警報に引き上げられました。",
							TsunamiLevel.Warning => "津波警報に引き上げられました。",
							TsunamiLevel.Advisory => "津波注意報に引き上げられました。",
							TsunamiLevel.Forecast => "津波予報が発表されています。",
							_ => "", // 存在しないはず
						});
					isUpdated = true;
				}
				else
				{
					UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
					if (Config.Notification.Tsunami)
						NotificationService?.Notify("津波情報", "津波情報が更新されました。");
					isUpdated = true;
				}
				if (!IsInitializing)
					MessageBus.Current.SendMessage(new TsunamiInformationUpdated(_current, value));
			}
			this.RaiseAndSetIfChanged(ref _current, value);
			if (TsunamiLayer != null)
				TsunamiLayer.Current = value;
			if (_current == null)
				MapPadding = new Avalonia.Thickness(0);
			else
				MapPadding = new Avalonia.Thickness(360, 0, 0, 0);

			if (isUpdated && Config.Tsunami.SwitchAtUpdate)
				ActiveRequest.Send(this);
		}
	}

	public override void Activating()
	{
		if (_control != null)
			return;
		_control = new TsunamiView
		{
			DataContext = this,
		};
	}
	public override void Deactivated() { }

	public async Task OpenXml()
	{
		if (App.MainWindow == null)
			return;

		try
		{
			var ofd = new OpenFileDialog();
			ofd.Filters.Add(new FileDialogFilter
			{
				Name = "防災情報XML",
				Extensions =
				[
					"xml"
				],
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
			(Current, FocusBound) = ProcessInformation(report);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "外部XMLの読み込みに失敗しました");
		}
	}

	public (TsunamiInfo?, Avalonia.Rect?) ProcessInformation(JmaXmlDocument report)
	{
		if (report.Control.Title != "津波警報・注意報・予報a")
			return (null, null);
		var tsunami = new TsunamiInfo
		{
			EventId = report.Head.EventId
		};
		if (report.Control.Status != "通常")
			tsunami.SpecialState = report.Control.Status;
		tsunami.ReportedAt = report.Head.ReportDateTime.DateTime;
		tsunami.ExpireAt = report.Head.ValidDateTime?.DateTime;

		var zoomPoints = new List<Location>();
		FeatureLayer? tsunamiLayer = null;
		MapData?.TryGetLayer(LandLayerType.TsunamiForecastArea, out tsunamiLayer);

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

			if (tsunamiLayer != null)
			{
				foreach (var p in tsunamiLayer.FindPolygon(i.Area.Code))
				{
					zoomPoints.Add(p.BoundingBox.TopLeft.CastLocation());
					zoomPoints.Add(p.BoundingBox.BottomRight.CastLocation());
				}
			}
		}
		if (forecastAreas.Count > 0)
			tsunami.ForecastAreas = forecastAreas.OrderBy(a => a.ArrivalTime).ToArray();
		if (advisoryAreas.Count > 0)
			tsunami.AdvisoryAreas = advisoryAreas.OrderBy(a => a.ArrivalTime).ToArray();
		if (warningAreas.Count > 0)
			tsunami.WarningAreas = warningAreas.OrderBy(a => a.ArrivalTime).ToArray();
		if (majorWarningAreas.Count > 0)
			tsunami.MajorWarningAreas = majorWarningAreas.OrderBy(a => a.ArrivalTime).ToArray();

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
			return (tsunami, new Avalonia.Rect(minLat - 1, minLng - 1, maxLat - minLat + 2, maxLng - minLng + 3));
		}

		return (tsunami, null);
	}
}
