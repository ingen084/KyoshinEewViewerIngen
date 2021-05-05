using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.KyoshinMonitor.RenderObjects;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor
{
	public class KyoshinMonitorSeries : SeriesBase
	{
		public KyoshinMonitorSeries() : base("強震モニタ")
		{
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
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.1, Color = System.Drawing.Color.FromArgb(255, 0, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.2, Color = System.Drawing.Color.FromArgb(255, 0, 255, 0) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.3, Color = System.Drawing.Color.FromArgb(255, 255, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.4, Color = System.Drawing.Color.FromArgb(255, 255, 255, 0) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.6, Color = System.Drawing.Color.FromArgb(255, 0, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.7, Color = System.Drawing.Color.FromArgb(255, 255, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.8, Color = System.Drawing.Color.FromArgb(255, 0, 0, 0) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 1.0, Color = System.Drawing.Color.FromArgb(255, 255, 0, 0) },
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

			IsSignalNowEewReceiving = SignalNowEewReceiveService.Default.CanReceive;

			MessageBus.Current.Listen<DisplayWarningMessageUpdated>().Subscribe(m => WarningMessage = m.Message);
			WorkingTime = DateTime.Now;

			MessageBus.Current.Listen<RealtimeDataParseProcessStarted>().Subscribe(t =>
			{
				IsWorking = true;
				WorkingTime = t.StartedTimerTime;
			});

			// EEW受信
			MessageBus.Current.Listen<EewUpdated>().Subscribe(e =>
			{
				var eews = e.Eews.Where(e => !e.IsCancelled && e.UpdatedTime - WorkingTime < TimeSpan.FromMilliseconds(ConfigurationService.Default.Timer.Offset * 2));
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
					psWaveCount++;
				}
				if (psWaveCount < EewRenderObjectCache.Count)
				{
					var c = EewRenderObjectCache.Count;
					for (int i = psWaveCount; i < c; i++)
					{
						TmpRealtimeRenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item1);
						TmpRenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item2);
						EewRenderObjectCache.RemoveAt(psWaveCount);
					}
				}
				Eews = eews.ToArray();
				RealtimeRenderObjects = TmpRealtimeRenderObjects.ToArray();
			});
			MessageBus.Current.Listen<RealtimeDataUpdated>().Subscribe(e =>
			{
				//var parseTime = DateTime.Now - WorkStartedTime;

				if (e.Data != null)
					foreach (var datum in e.Data)
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
						item.IntensityColor = new SKColor(datum.Color.R, datum.Color.G, datum.Color.B, 255);
					}
				RealtimePoints = e.Data?.OrderByDescending(p => p.AnalysisResult ?? -1000, null);

				if (e.Data != null)
					WarningMessage = null;
				//IsImage = e.IsUseAlternativeSource;
				IsWorking = false;
				CurrentTime = e.Time;
				RenderObjects = TmpRenderObjects.ToArray();

				//logger.Trace($"Time: {parseTime.TotalMilliseconds:.000},{(DateTime.Now - WorkStartedTime - parseTime).TotalMilliseconds:.000}");
			});

			ConfigurationService.Default.Timer.WhenAnyValue(x => x.TimeshiftSeconds).Subscribe(x => IsReplay = x < 0);
			ConfigurationService.Default.KyoshinMonitor.WhenAnyValue(x => x.HideShindoIcon).Subscribe(x => UseShindoIcon = !x);
			UseShindoIcon = !ConfigurationService.Default.KyoshinMonitor.HideShindoIcon;

			KyoshinMonitorWatchService.Default.Start();
		}

		public override void Deactivated()
		{
			IsActivate = false;
		}


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
			set
			{
				this.RaiseAndSetIfChanged(ref _realtimePoints, value);
				this.RaisePropertyChanged(nameof(RealtimePointCounts));
			}
		}

		[Reactive]
		public bool UseShindoIcon { get; set; } = true;

		#region realtimePoint
		public List<IRenderObject> TmpRenderObjects { get; } = new List<IRenderObject>();
		public List<RealtimeRenderObject> TmpRealtimeRenderObjects { get; } = new List<RealtimeRenderObject>();
		#endregion

		private Dictionary<string, RawIntensityRenderObject> RenderObjectMap { get; } = new();
		private List<(EewPSWaveRenderObject, EewCenterRenderObject)> EewRenderObjectCache { get; } = new();
		private DateTime WorkingTime { get; set; }
	}
}
