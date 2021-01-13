using KyoshinEewViewer.Extensions;
using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.Models;
using KyoshinEewViewer.Models.Events;
using KyoshinEewViewer.Properties;
using KyoshinEewViewer.RenderObjects;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.ApiResult.AppApi;
using MessagePack;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
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

		private IEnumerable<LinkedRealtimeData> _realtimePoints;

		public int RealtimePointCounts => RealtimePoints?.Count() ?? 0;
		public IEnumerable<LinkedRealtimeData> RealtimePoints
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
		internal IEventAggregator EventAggregator { get; }

		private IDialogService DialogService { get; }

		public MainWindowViewModel(
			ConfigurationService configService,
			KyoshinMonitorWatchService monitorService,
			LoggerService logger,
			TrTimeTableService trTimeTableService,
			UpdateCheckService updateCheckService,
			JmaXmlPullReceiveService jmaXmlPullReceiver,
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
						if (!RenderObjectMap.ContainsKey(datum.GetPointIdentity()))
						{
							var render = new RawIntensityRenderObject(datum.ObservationPoint.Point?.Location ?? new Location(datum.ObservationPoint.Site.Lat, datum.ObservationPoint.Site.Lng),
								datum.ObservationPoint.Point?.Name ?? datum.ObservationPoint.Site?.Prefefecture.GetLongName() + "/不明");
							RenderObjects.Add(render);
							RenderObjectMap.Add(datum.GetPointIdentity(), render);
						}
						var item = RenderObjectMap[datum.GetPointIdentity()];
						lock (item)
							item.RawIntensity = datum.Value ?? float.NaN;
					}
				RealtimePoints = e.Data?.OrderByDescending(p => p.Value ?? -1000, null);

				if (e.Data != null)
					WarningMessage = null;
				IsImage = e.IsUseAlternativeSource;
				IsWorking = false;
				CurrentTime = e.Time;
				ConfirmedRenderObjects = RenderObjects.ToArray();
				ConfirmedRealtimeRenderObjects = RealtimeRenderObjects.ToArray();

				logger.Trace($"Time: {parseTime.TotalMilliseconds:.000},{(DateTime.Now - WorkStartedTime - parseTime).TotalMilliseconds:.000}");
			});
			monitorService.Start();

			aggregator.GetEvent<UpdateFound>().Subscribe(b => UpdateAvailable = b);
			aggregator.GetEvent<ShowSettingWindowRequested>().Subscribe(() => ShowSettingWindowCommand.Execute(null));

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

			aggregator.GetEvent<EarthquakeUpdated>().Subscribe(e =>
			{
				Earthquakes.Clear();
				Earthquakes.AddRange(jmaXmlPullReceiver.Earthquakes);
				RaisePropertyChanged(nameof(FirstEarthquake));
				RaisePropertyChanged(nameof(SubEarthquakes));
			});
			jmaXmlPullReceiver.Initalize();

			Map = MessagePackSerializer.Deserialize<TopologyMap>(Resources.WorldMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
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
					Place = "これはサンプルデータです"
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 100,
					Intensity = JmaIntensity.Int4,
					Magnitude = 6.1f,
					Place = "デザイナ"
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 100,
					Intensity = JmaIntensity.Int5Lower,
					Magnitude = 3.0f,
					Place = "サンプル"
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 100,
					Intensity = JmaIntensity.Int6Upper,
					Magnitude = 6.1f,
					Place = "ViewModel"
				},
				new Earthquake
				{
					OccurrenceTime = DateTime.Now,
					Depth = 100,
					Intensity = JmaIntensity.Int7,
					Magnitude = 6.1f,
					Place = "です"
				}
			};

			var points = new List<LinkedRealtimeData>()
			{
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 0),
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 1),
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 2),
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 3),
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 4),
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 5),
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 6),
				new LinkedRealtimeData(new LinkedObservationPoint(new Site(){ PrefefectureId = 27 }, new ObservationPoint{ Region = "テスト", Name = "テスト" }), 7),
			};

			RealtimePoints = points.OrderByDescending(p => p.Value ?? -1000, null);

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