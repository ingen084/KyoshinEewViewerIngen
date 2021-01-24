using KyoshinEewViewer.Extensions;
using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.Models;
using KyoshinEewViewer.Models.Events;
using KyoshinEewViewer.Properties;
using KyoshinEewViewer.RenderObjects;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.ApiResult.AppApi;
using KyoshinMonitorLib.Images;
using MessagePack;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;

namespace KyoshinEewViewer.ViewModels
{
	public class MainWindowViewModel : BindableBase
	{
		private string _title = "KyoshinEewViewer for ingen";

		public string Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		private string version = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString()
#if DEBUG
			+ " DEBUG"
#endif
			;

		public string Version
		{
			get => version;
			set => SetProperty(ref version, value);
		}

		private double windowScale = 1;
		public double WindowScale
		{
			get => windowScale;
			set => SetProperty(ref windowScale, value);
		}

		#region 警告メッセージ

		private string warningMessage;

		public string WarningMessage
		{
			get => warningMessage;
			set
			{
				SetProperty(ref warningMessage, value);
				RaisePropertyChanged(nameof(CanShowWarningMessage));
			}
		}

		public bool CanShowWarningMessage => !string.IsNullOrWhiteSpace(WarningMessage);

		#endregion 警告メッセージ

		#region 上部時刻表示とか

		private bool isWorking;

		public bool IsWorking
		{
			get => isWorking;
			set => SetProperty(ref isWorking, value);
		}

		private bool isImage;

		public bool IsImage
		{
			get => isImage;
			set => SetProperty(ref isImage, value);
		}

		private DateTime currentTime = DateTime.Now;

		public DateTime CurrentTime
		{
			get => currentTime;
			set => SetProperty(ref currentTime, value);
		}

		private string currentImageType = "強震モニタ リアルタイム震度";

		public string CurrentImageType
		{
			get => currentImageType;
			set => SetProperty(ref currentImageType, value);
		}

		private bool isReplay;

		public bool IsReplay
		{
			get => isReplay;
			set => SetProperty(ref isReplay, value);
		}

		#endregion 上部時刻表示とか

		#region 更新情報

		private bool updateAvailable;

		public bool UpdateAvailable
		{
			get => updateAvailable;
			set => SetProperty(ref updateAvailable, value);
		}

		private ICommand _showUpdateInfoWindowCommand;
		public ICommand ShowUpdateInfoWindowCommand => _showUpdateInfoWindowCommand ??= new DelegateCommand(() => DialogService.Show("UpdateInfoWindow"));

		#endregion 更新情報

		#region 設定ウィンドウ

		private ICommand _showSettingWindowCommand;
		public ICommand ShowSettingWindowCommand => _showSettingWindowCommand ??= new DelegateCommand(() => DialogService.Show("SettingWindow"));

		#endregion 設定ウィンドウ

		#region 地震情報
		private string earthquakeSource = "不明";
		public string EarthquakeSource
		{
			get => earthquakeSource;
			set => SetProperty(ref earthquakeSource, value);
		}

		private List<Earthquake> earthquakes = new List<Earthquake>();

		public List<Earthquake> Earthquakes
		{
			get => earthquakes;
			set
			{
				SetProperty(ref earthquakes, value);
				RaisePropertyChanged(nameof(SubEarthquakes));
				RaisePropertyChanged(nameof(FirstEarthquake));
			}
		}

		public IEnumerable<Earthquake> SubEarthquakes => Earthquakes.Skip(1);
		public Earthquake FirstEarthquake => Earthquakes.FirstOrDefault();

		#endregion 地震情報

		#region EEW

		private Eew[] eews = Array.Empty<Eew>();

		public Eew[] Eews
		{
			get => eews;
			set => SetProperty(ref eews, value);
		}

		#endregion EEW

		#region 最大観測地点

		private IEnumerable<ImageAnalysisResult> _realtimePoints;

		public int RealtimePointCounts => RealtimePoints?.Count() ?? 0;
		public IEnumerable<ImageAnalysisResult> RealtimePoints
		{
			get => _realtimePoints;
			set
			{
				SetProperty(ref _realtimePoints, value);
				RaisePropertyChanged(nameof(RealtimePointCounts));
				//RaisePropertyChanged(nameof(FirstRealtimePoint));
				//RaisePropertyChanged(nameof(SubRealtimePoints));
			}
		}

		//public IEnumerable<LinkedRealtimeData> SubRealtimePoints => RealtimePoints?.Skip(1).Take(30);
		//public LinkedRealtimeData? FirstRealtimePoint => RealtimePoints?.FirstOrDefault();

		private bool useShindoIcon = true;
		public bool UseShindoIcon
		{
			get => useShindoIcon;
			set => SetProperty(ref useShindoIcon, value);
		}

		#endregion 最大観測地点

		#region Map
		private TopologyMap map;
		public TopologyMap Map
		{
			get => map;
			set => SetProperty(ref map, value);
		}

		public List<IRenderObject> RenderObjects { get; } = new List<IRenderObject>();
		private IRenderObject[] confirmedRenderObjects;
		public IRenderObject[] ConfirmedRenderObjects
		{
			get => confirmedRenderObjects;
			set => SetProperty(ref confirmedRenderObjects, value);
		}

		public List<RealtimeRenderObject> RealtimeRenderObjects { get; } = new List<RealtimeRenderObject>();
		private RealtimeRenderObject[] confirmedRealtimeRenderObjects;
		public RealtimeRenderObject[] ConfirmedRealtimeRenderObjects
		{
			get => confirmedRealtimeRenderObjects;
			set => SetProperty(ref confirmedRealtimeRenderObjects, value);
		}

		private Location centerLocation;
		public Location CenterLocation
		{
			get => centerLocation;
			set => SetProperty(ref centerLocation, value);
		}

		private double zoom;
		public double Zoom
		{
			get => zoom;
			set { _ = SetProperty(ref zoom, value); }
		}
		#endregion Map

		private Dictionary<string, RawIntensityRenderObject> RenderObjectMap { get; } = new Dictionary<string, RawIntensityRenderObject>();

		private List<(EewPSWaveRenderObject, EewCenterRenderObject)> EewRenderObjectCache { get; } = new List<(EewPSWaveRenderObject, EewCenterRenderObject)>();

		private DateTime WorkStartedTime { get; set; }

		internal ConfigurationService ConfigService { get; }
		internal PullEarthquakeInfoService EarthquakeInfoService { get; }
		internal IEventAggregator EventAggregator { get; }

		private IDialogService DialogService { get; }

		public MainWindowViewModel(
			ConfigurationService configService,
			KyoshinMonitorWatchService monitorService,
			LoggerService logger,
			TrTimeTableService trTimeTableService,
			UpdateCheckService updateCheckService,
			PullEarthquakeInfoService pullEarthquakeInfoService,
			IEventAggregator aggregator,
			IDialogService dialogService)
		{
			ConfigService = configService;
			updateCheckService.StartUpdateCheckTask();

			DialogService = dialogService;

			logger.WarningMessageUpdated += m => WarningMessage = m;
			WorkStartedTime = DateTime.Now;

			EventAggregator = aggregator;
			aggregator.GetEvent<RealtimeDataParseProcessStarted>().Subscribe(t =>
			{
				IsWorking = true;
				WorkStartedTime = DateTime.Now;
			});

			// EEW受信
			aggregator.GetEvent<EewUpdated>().Subscribe(e =>
			{
				var psWaveCount = 0;
				foreach (var eew in e.Eews.Where(e => !e.IsCancelled))
				{
					if (EewRenderObjectCache.Count <= psWaveCount)
					{
						var wave = new EewPSWaveRenderObject(trTimeTableService, currentTime, eew);
						var co = new EewCenterRenderObject(new Location(0, 0));
						RealtimeRenderObjects.Insert(0, wave);
						RenderObjects.Add(co);
						EewRenderObjectCache.Add((wave, co));
					}

					(var w, var c) = EewRenderObjectCache[psWaveCount];
					lock (w)
						w.Eew = eew;
					lock (c)
						c.Location = eew.Location;
					psWaveCount++;
				}
				if (psWaveCount < EewRenderObjectCache.Count)
				{
					var c = EewRenderObjectCache.Count;
					for (int i = psWaveCount; i < c; i++)
					{
						RealtimeRenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item1);
						RenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item2);
						EewRenderObjectCache.RemoveAt(psWaveCount);
					}
				}
				Eews = e.Eews.ToArray();
			});
			aggregator.GetEvent<RealtimeDataUpdated>().Subscribe(e =>
			{
				var parseTime = DateTime.Now - WorkStartedTime;

				foreach (var obj in RenderObjectMap.Values)
					obj.RawIntensity = float.NaN;

				if (e.Data?.Any() ?? false)
					foreach (var datum in e.Data)
					{
						if (!RenderObjectMap.ContainsKey(datum.ObservationPoint.Code))
						{
							var render = new RawIntensityRenderObject(datum.ObservationPoint?.Location, datum.ObservationPoint?.Name);
							RenderObjects.Add(render);
							RenderObjectMap.Add(datum.ObservationPoint.Code, render);
						}
						var item = RenderObjectMap[datum.ObservationPoint.Code];
						lock (item)
						{
						}

						item.RawIntensity = datum.GetResultToIntensity() ?? float.NaN;
						// 描画用の色を設定する
						item.IntensityColor = Color.FromRgb(datum.Color.R, datum.Color.G, datum.Color.B);
					}
				RealtimePoints = e.Data?.OrderByDescending(p => p.AnalysisResult ?? -1000, null);

				if (e.Data != null)
					WarningMessage = null;
				//IsImage = e.IsUseAlternativeSource;
				IsWorking = false;
				CurrentTime = e.Time;
				ConfirmedRenderObjects = RenderObjects.ToArray();
				ConfirmedRealtimeRenderObjects = RealtimeRenderObjects.ToArray();

				logger.Trace($"Time: {parseTime.TotalMilliseconds:.000},{(DateTime.Now - WorkStartedTime - parseTime).TotalMilliseconds:.000}");
			});
			monitorService.Start();

			aggregator.GetEvent<UpdateFound>().Subscribe(b => UpdateAvailable = b);
			aggregator.GetEvent<ShowSettingWindowRequested>().Subscribe(() => ShowSettingWindowCommand.Execute(null));

			ConfigService.Configuration.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName != nameof(ConfigService.Configuration.WindowScale))
					return;
				WindowScale = ConfigService.Configuration.WindowScale;
			};
			WindowScale = ConfigService.Configuration.WindowScale;

			ConfigService.Configuration.Timer.PropertyChanged += (s, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(ConfigService.Configuration.Timer.TimeshiftSeconds):
						IsReplay = ConfigService.Configuration.Timer.TimeshiftSeconds < 0;
						break;
				}
			};
			ConfigService.Configuration.KyoshinMonitor.PropertyChanged += (s, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(ConfigService.Configuration.KyoshinMonitor.HideShindoIcon):
						UseShindoIcon = !ConfigService.Configuration.KyoshinMonitor.HideShindoIcon;
						break;
				}
			};
			UseShindoIcon = !ConfigService.Configuration.KyoshinMonitor.HideShindoIcon;

			Earthquakes.Add(new Earthquake
			{
				OccurrenceTime = DateTime.MinValue,
				Depth = 0,
				Intensity = JmaIntensity.Unknown,
				Magnitude = 0,
				Place = "受信中...",
			});

			EarthquakeInfoService = pullEarthquakeInfoService;
			aggregator.GetEvent<EarthquakeUpdated>().Subscribe(e =>
			{
				Earthquakes.Clear();
				Earthquakes.AddRange(EarthquakeInfoService.Earthquakes);
				RaisePropertyChanged(nameof(FirstEarthquake));
				RaisePropertyChanged(nameof(SubEarthquakes));
			});
			aggregator.GetEvent<DmdataStatusUpdated>().Subscribe(UpdateDmdataStatus);
			EarthquakeInfoService.InitalizeAsync().ConfigureAwait(false);

			Map = MessagePackSerializer.Deserialize<TopologyMap>(Resources.WorldMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

		}

		private void UpdateDmdataStatus()
		{
			EarthquakeSource = EarthquakeInfoService.DmdataService.Status switch
			{
				DmdataStatus.Stopping => "気象庁防災情報XML",
				DmdataStatus.StoppingForInvalidKey => "気象庁防災情報XML",
				DmdataStatus.Failed => "気象庁防災情報XML",
				DmdataStatus.UsingPullForForbidden => "Project DM-D.S.S/PULL型",
				DmdataStatus.UsingPullForError => "Project DM-D.S.S/PULL型",
				DmdataStatus.UsingPull => "Project DM-D.S.S/PULL型",
				DmdataStatus.ReconnectingWebSocket => "Project DM-D.S.S/WebSocket再接続中",
				DmdataStatus.UsingWebSocket => "Project DM-D.S.S/WebSocket",
				DmdataStatus.Initalizing => "Project DM-D.S.S/初期化中",
				_ => "不明",
			};
		}

#if DEBUG
		public MainWindowViewModel()
		{
			CurrentTime = DateTime.Now;

			IsWorking = true;

			WarningMessage = "これは けいこくめっせーじ じゃ！";

			CurrentImageType = "種別";
			Earthquakes = new List<Earthquake>
			{
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 10,
					Intensity = JmaIntensity.Int0,
					Magnitude = 3.1f,
					Place = "これはサンプルデータです",
					IsVeryShallow = true
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 30,
					Intensity = JmaIntensity.Int4,
					Magnitude = 6.1f,
					Place = "デザイナ"
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 60,
					Intensity = JmaIntensity.Int5Lower,
					Magnitude = 3.0f,
					Place = "サンプル"
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 90,
					Intensity = JmaIntensity.Int6Upper,
					Magnitude = 6.1f,
					Place = "ViewModel"
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 450,
					Intensity = JmaIntensity.Int7,
					Magnitude = 6.1f,
					Place = "です"
				}
			};

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
				new Eew
				{
					Intensity = JmaIntensity.Int3,
					IsWarning = false,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Depth = 0,
					Place = "通常テスト",
				},
				new Eew
				{
					Intensity = JmaIntensity.Int4,
					IsWarning = true,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Magnitude = 1.0f,
					Depth = 10,
					Place = "PLUMテスト",
				},
				new Eew
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

			Map = MessagePackSerializer.Deserialize<TopologyMap>(Resources.WorldMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			Zoom = 5;
			CenterLocation = new Location(36.474f, 135.264f);

			ConfirmedRenderObjects = new IRenderObject[]
			{
				//new EewPSWaveRenderObject(new Location(34.6829f, 133.6015f), 500000, null, new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 160, 255)), 1)),
				//new EewPSWaveRenderObject(new Location(34.6829f, 133.6015f), 300000, new SolidColorBrush(Color.FromArgb(30, 255, 80, 120)), new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 80, 120)), 1)),
				new RawIntensityRenderObject(new Location(34.6829f, 135.6015f), "謎", 4),
				new EewCenterRenderObject(new Location(34.6829f, 133.6015f))
			};
		}
#endif
	}
}