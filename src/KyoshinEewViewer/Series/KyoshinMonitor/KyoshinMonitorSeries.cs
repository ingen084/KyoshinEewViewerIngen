using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor
{
	public class KyoshinMonitorSeries : SeriesBase
	{
		public KyoshinMonitorSeries() : base("強震モニタ")
		{
			MapPadding = new Avalonia.Rect(0, 0, 300, 0);

			#region dev用モック
#if DEBUG
			CurrentTime = DateTime.Now;

			IsWorking = true;
			IsSignalNowEewReceiving = true;
			IsLast10SecondsEewReceiving = false;

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

		[Reactive]
		public Eew[] Eews { get; set; } = Array.Empty<Eew>();

		private IEnumerable<ImageAnalysisResult> _realtimePoints = Array.Empty<ImageAnalysisResult>();
		public int RealtimePointCounts => RealtimePoints?.Count(p => p.AnalysisResult != null) ?? 0;
		public IEnumerable<ImageAnalysisResult> RealtimePoints
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

		//private Dictionary<string, RawIntensityRenderObject> RenderObjectMap { get; } = new();
		//private List<(EewPSWaveRenderObject, EewCenterRenderObject)> EewRenderObjectCache { get; } = new();
		private DateTime WorkingTime { get; set; }
	}
}
