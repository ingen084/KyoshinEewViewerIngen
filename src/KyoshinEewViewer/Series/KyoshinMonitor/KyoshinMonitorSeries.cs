using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.RenderObjects;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.SkiaImages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor
{
	public class KyoshinMonitorSeries : SeriesBase
	{
		public KyoshinMonitorSeries(NotificationService? notificationService = null) : base("強震モニタ")
		{
			NotificationService	= notificationService ?? Locator.Current.GetService<NotificationService>() ?? throw new Exception("NotificationServiceの解決に失敗しました");
			EewControler = new EewControlService(NotificationService);
			KyoshinMonitorWatcher = new KyoshinMonitorWatchService(EewControler);
			SignalNowEewReceiver = new SignalNowEewReceiveService(EewControler);
			MapPadding = new Thickness(0, 0, 300, 0);

			#region dev用モック
			if (Design.IsDesignMode)
			{
#if DEBUG
				CurrentTime = DateTime.Now;

				IsWorking = true;
				IsSignalNowEewReceiving = true;
				IsLast10SecondsEewReceiving = false;

				WarningMessage = "これは けいこくめっせーじ じゃ！";

				var points = new List<ImageAnalysisResult>()
			{
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.1, Color = new SKColor(255, 0, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.2, Color = new SKColor(0, 255, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.3, Color = new SKColor(255, 0, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.4, Color = new SKColor(255, 255, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.6, Color = new SKColor(0, 255, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.7, Color = new SKColor(255, 255, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.8, Color = new SKColor(0, 0, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 1.0, Color = new SKColor(255, 0, 0, 255) },
			};

				RealtimePoints = points.OrderByDescending(p => p.AnalysisResult ?? -1, null);

				Eews = new[]
				{
				new Eew(EewSource.NIED, "a")
				{
					Intensity = JmaIntensity.Int3,
					IsWarning = false,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Depth = 0,
					Place = "通常テスト",
				},
				new Eew(EewSource.NIED, "b")
				{
					Intensity = JmaIntensity.Int5Lower,
					IsWarning = true,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Magnitude = 1.0f,
					Depth = 10,
					Place = "PLUMテスト",
				},
				new Eew(EewSource.NIED, "c")
				{
					Intensity = JmaIntensity.Unknown,
					IsWarning = false,
					IsCancelled = true,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Depth = 0,
					Place = "キャンセルテスト",
				}
			};

				IsReplay = true;
#endif
				return;
			}
			#endregion
		}

		private EewControlService EewControler { get; }
		private NotificationService NotificationService { get; }
		private KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
		private SignalNowEewReceiveService SignalNowEewReceiver { get; }

		private KyoshinMonitorView? control;
		public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

		public bool IsActivate { get; set; }

		public override void Activating()
		{
			IsActivate = true;
			if (control != null)
				return;
			//TODO サービス初期化･ハンドラ定義
			control = new KyoshinMonitorView
			{
				DataContext = this
			};

			if (Design.IsDesignMode)
				return;

			MessageBus.Current.Listen<DisplayWarningMessageUpdated>().Subscribe(m => WarningMessage = m.Message);
			WorkingTime = DateTime.Now;

			MessageBus.Current.Listen<RealtimeDataParseProcessStarted>().Subscribe(t =>
			{
				IsWorking = true;
				WorkingTime = t.StartedTimerTime;
			});

			// EEW受信
			EewControler.EewUpdated += e =>
			{
				var eews = e.eews.Where(e => !e.IsCancelled && e.UpdatedTime - WorkingTime < TimeSpan.FromMilliseconds(ConfigurationService.Current.Timer.Offset * 2));
				var psWaveCount = 0;
				foreach (var eew in eews)
				{
					if (EewRenderObjectCache.Count <= psWaveCount)
					{
						var wave = new EewPSWaveRenderObject(CurrentTime, eew);
						var co = new EewCenterRenderObject(new KyoshinMonitorLib.Location(0, 0), eew.IsUnreliableLocation);
						TmpRealtimeRenderObjects.Insert(0, wave);
						TmpRenderObjects.Add(co);
						EewRenderObjectCache.Add((wave, co));
					}

					(var w, var c) = EewRenderObjectCache[psWaveCount];
					w.Eew = eew;
					c.Location = eew.Location;
					c.IsUnreliable = eew.IsUnreliableLocation;
					psWaveCount++;
				}
				if (psWaveCount < EewRenderObjectCache.Count)
				{
					var c = EewRenderObjectCache.Count;
					for (var i = psWaveCount; i < c; i++)
					{
						TmpRealtimeRenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item1);
						TmpRenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item2);
						EewRenderObjectCache.RemoveAt(psWaveCount);
					}
				}
				Eews = eews.ToArray();
				RealtimeRenderObjects = TmpRealtimeRenderObjects.ToArray();
			};
			KyoshinMonitorWatcher.RealtimeDataUpdated += e =>
			{
				//var parseTime = DateTime.Now - WorkStartedTime;

				if (e.data != null)
					foreach (var datum in e.data)
					{
						if (datum.ObservationPoint == null)
							continue;

						if (!RenderObjectMap.TryGetValue(datum.ObservationPoint.Code, out var item))
						{
							// 描画対象じゃなかった観測点がnullの場合そもそも登録しない
							if (datum.AnalysisResult == null)
								continue;
							item = new RawIntensityRenderObject(datum.ObservationPoint.Location, datum.ObservationPoint.Name);
							TmpRenderObjects.Add(item);
							RenderObjectMap.Add(datum.ObservationPoint.Code, item);
						}

						item.RawIntensity = datum.GetResultToIntensity() ?? double.NaN;
						// 描画用の色を設定する
						item.IntensityColor = datum.Color;
					}
				RealtimePoints = e.data?.OrderByDescending(p => p.AnalysisResult ?? -1000, null);

				if (e.data != null)
					WarningMessage = null;
				//IsImage = e.IsUseAlternativeSource;
				IsWorking = false;
				CurrentTime = e.time;
				RenderObjects = TmpRenderObjects.ToArray();

				// 強震モニタの時刻に補正する
				foreach (var obj in EewRenderObjectCache)
					obj.Item1.BaseTime = e.time;
				//logger.Trace($"Time: {parseTime.TotalMilliseconds:.000},{(DateTime.Now - WorkStartedTime - parseTime).TotalMilliseconds:.000}");
			};

			IsSignalNowEewReceiving = SignalNowEewReceiver.CanReceive;

			ConfigurationService.Current.Timer.WhenAnyValue(x => x.TimeshiftSeconds).Subscribe(x => IsReplay = x < 0);
			ConfigurationService.Current.KyoshinMonitor.WhenAnyValue(x => x.ListRenderMode)
				.Subscribe(x => ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(ConfigurationService.Current.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode);
			ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(ConfigurationService.Current.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode;

			Task.Run(() => KyoshinMonitorWatcher.Start());
		}

		public override void Deactivated() => IsActivate = false;


		#region 上部時刻表示とか
		[Reactive]
		public bool IsWorking { get; set; }

		[Reactive]
		public DateTime CurrentTime { get; set; } = DateTime.Now;

		[Reactive]
		public bool IsReplay { get; set; }

		[Reactive]
		public bool IsSignalNowEewReceiving { get; set; }
		[Reactive]
		public bool IsLast10SecondsEewReceiving { get; set; }
		#endregion 上部時刻表示とか

		#region 警告メッセージ

		public string? WarningMessage { get; set; }

		#endregion 警告メッセージ

		[Reactive]
		public Eew[] Eews { get; set; } = Array.Empty<Eew>();

		private IEnumerable<ImageAnalysisResult>? _realtimePoints = Array.Empty<ImageAnalysisResult>();
		public int RealtimePointCounts => RealtimePoints?.Count(p => p.AnalysisResult != null) ?? 0;
		public IEnumerable<ImageAnalysisResult>? RealtimePoints
		{
			get => _realtimePoints;
			set {
				this.RaiseAndSetIfChanged(ref _realtimePoints, value);
				this.RaisePropertyChanged(nameof(RealtimePointCounts));
			}
		}

		[Reactive]
		public RealtimeDataRenderMode ListRenderMode { get; set; } = RealtimeDataRenderMode.ShindoIcon;

		#region realtimePoint
		public List<IRenderObject> TmpRenderObjects { get; } = new List<IRenderObject>();
		public List<RealtimeRenderObject> TmpRealtimeRenderObjects { get; } = new List<RealtimeRenderObject>();
		#endregion

		private Dictionary<string, RawIntensityRenderObject> RenderObjectMap { get; } = new();
		private List<(EewPSWaveRenderObject, EewCenterRenderObject)> EewRenderObjectCache { get; } = new();
		private DateTime WorkingTime { get; set; }
	}
}
