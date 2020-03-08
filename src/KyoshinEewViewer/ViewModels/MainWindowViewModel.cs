using KyoshinEewViewer.Extensions;
using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.RenderObjects;
using KyoshinEewViewer.Models;
using KyoshinEewViewer.Properties;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.ApiResult.AppApi;
using MessagePack;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

		#region 可視状態

		private WindowState windowState;

		public WindowState WindowState
		{
			get => windowState;
			set
			{
				if (value == windowState)
					return;
				if (ConfigService.Configuration.Notification.Enable
					&& ConfigService.Configuration.Notification.HideWhenMinimizeWindow
					&& value == WindowState.Minimized)
				{
					WindowVisibility = Visibility.Collapsed;
					return;
				}
				SetProperty(ref windowState, value);
			}
		}

		private Visibility windowVisibility;

		public Visibility WindowVisibility
		{
			get => windowVisibility;
			set
			{
				if (SetProperty(ref windowVisibility, value))
				{
					if (value == Visibility.Collapsed)
						ShowInTaskbar = false;
					else
					{
						ShowInTaskbar = true;
						ShowWindowRequest?.Raise(null);
					}
				}
			}
		}

		private bool showInTaskbar = true;

		public bool ShowInTaskbar
		{
			get => showInTaskbar;
			set => SetProperty(ref showInTaskbar, value);
		}

#pragma warning disable CS0618 // 型またはメンバーが旧型式です
		public InteractionRequest<Notification> ShowWindowRequest { get; set; } = new InteractionRequest<Notification>();
#pragma warning restore CS0618 // 型またはメンバーが旧型式です

		#endregion 可視状態

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

		private string currentImageType = "リアルタイム震度";

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

#pragma warning disable CS0618 // 型またはメンバーが旧型式です
		public InteractionRequest<Notification> ShowUpdateInfoWindowRequest { get; set; } = new InteractionRequest<Notification>();
		private ICommand _showUpdateInfoWindowCommand;
		public ICommand ShowUpdateInfoWindowCommand => _showUpdateInfoWindowCommand ?? (_showUpdateInfoWindowCommand = new DelegateCommand(() => ShowUpdateInfoWindowRequest.Raise(new Notification())));
#pragma warning restore CS0618 // 型またはメンバーが旧型式です

		#endregion 更新情報

		#region 設定ウィンドウ

#pragma warning disable CS0618 // 型またはメンバーが旧型式です
		public InteractionRequest<Notification> ShowSettingWindowRequest { get; set; } = new InteractionRequest<Notification>();
		private ICommand _showSettingWindowCommand;
		public ICommand ShowSettingWindowCommand => _showSettingWindowCommand ?? (_showSettingWindowCommand = new DelegateCommand(() => ShowSettingWindowRequest.Raise(new Notification())));
#pragma warning restore CS0618 // 型またはメンバーが旧型式です

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

		private Eew[] eews = new Eew[0];

		public Eew[] Eews
		{
			get => eews;
			set => SetProperty(ref eews, value);
		}

		#endregion EEW

		#region 最大観測地点

		private IOrderedEnumerable<LinkedRealTimeData> _realtimePoints;

		private IOrderedEnumerable<LinkedRealTimeData> RealtimePoints
		{
			get => _realtimePoints;
			set
			{
				_realtimePoints = value;
				RaisePropertyChanged(nameof(FirstRealtimePoint));
				RaisePropertyChanged(nameof(SubRealtimePoints));
			}
		}

		public IEnumerable<LinkedRealTimeData> SubRealtimePoints => RealtimePoints?.Skip(1).Take(30);
		public LinkedRealTimeData? FirstRealtimePoint => RealtimePoints?.FirstOrDefault();

		#endregion 最大観測地点

		#region Map
		private TopologyMap map;
		public TopologyMap Map
		{
			get => map;
			set => SetProperty(ref map, value);
		}

		public List<RenderObject> RenderObjects { get; } = new List<RenderObject>();
		private RenderObject[] confirmedRenderObjects;
		public RenderObject[] ConfirmedRenderObjects
		{
			get => confirmedRenderObjects;
			set => SetProperty(ref confirmedRenderObjects, value);
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

		private List<(EllipseRenderObject, EllipseRenderObject, EewCenterRenderObject)> EewRenderObjectCache { get; } = new List<(EllipseRenderObject, EllipseRenderObject, EewCenterRenderObject)>();

		private DateTime WorkStartedTime { get; set; }

		internal ConfigurationService ConfigService { get; }
		internal IEventAggregator EventAggregator { get; }

		public MainWindowViewModel(
			ConfigurationService configService,
			KyoshinMonitorWatchService monitorService,
			LoggerService logger,
			TrTimeTableService trTimeTableService,
			ThemeService _,
			UpdateCheckService updateCheckService,
			NotifyIconService notifyIconService,
			JmaXmlPullReceiveService jmaXmlPullReceiver,
			IEventAggregator aggregator)
		{
			ConfigService = configService;
			updateCheckService.StartUpdateCheckTask();

			logger.WarningMessageUpdated += m => WarningMessage = m;
			WorkStartedTime = DateTime.Now;

			EventAggregator = aggregator;
			aggregator.GetEvent<Events.ShowMainWindowRequested>().Subscribe(() =>
			{
				WindowVisibility = Visibility.Visible;
				WindowState = WindowState.Normal;
			});
			aggregator.GetEvent<Events.TimeElapsed>().Subscribe(t =>
			{
				IsWorking = true;
				WorkStartedTime = DateTime.Now;
			});
			aggregator.GetEvent<Events.EewUpdated>().Subscribe(e =>
			{
				var psWaveCount = 0;
				foreach (var eew in e.Eews)
				{
					if (EewRenderObjectCache.Count <= psWaveCount)
					{
						var po = new EllipseRenderObject(new Location(0, 0), 0, null, new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 160, 255)), 1));
						var so = new EllipseRenderObject(new Location(0, 0), 0, new RadialGradientBrush(new GradientStopCollection(new[] { new GradientStop(Color.FromArgb(0, 255, 80, 120), .6), new GradientStop(Color.FromArgb(80, 255, 80, 120), 1) })), new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 80, 120)), 1));
						var co = new EewCenterRenderObject(new Location(0, 0));
						RenderObjects.Insert(0, po);
						RenderObjects.Insert(0, so);
						RenderObjects.Add(co);
						EewRenderObjectCache.Add((po, so, co));
					}
					(var p, var s, var c) = EewRenderObjectCache[psWaveCount];

					(var pDistance, var sDistance) = trTimeTableService.CalcDistance(eew.OccurrenceTime, e.Time, eew.Depth);
					lock (p)
					{
						p.Radius = (pDistance ?? 0) * 1000;
						p.Center = eew.Location;
					}
					lock (s)
					{
						s.Radius = (sDistance ?? 0) * 1000;
						s.Center = eew.Location;
					}
					lock (c)
						c.Location = eew.Location;
					psWaveCount++;
				}
				if (psWaveCount < EewRenderObjectCache.Count)
				{
					var c = EewRenderObjectCache.Count;
					for (int i = psWaveCount; i < c; i++)
					{
						RenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item1);
						RenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item2);
						RenderObjects.Remove(EewRenderObjectCache[psWaveCount].Item3);
						EewRenderObjectCache.RemoveAt(psWaveCount);
					}
				}
			});
			aggregator.GetEvent<Events.RealTimeDataUpdated>().Subscribe(e =>
			{
				var parseTime = DateTime.Now - WorkStartedTime;

				foreach (var obj in RenderObjectMap.Values)
					obj.RawIntensity = float.NaN;

				if (e.Data?.Any() ?? false)
					foreach (var datum in e.Data)
					{
						if (!RenderObjectMap.ContainsKey(datum.GetPointHash()))
						{
							var render = new RawIntensityRenderObject(datum.ObservationPoint.Point?.Location ?? new Location(datum.ObservationPoint.Site.Lat, datum.ObservationPoint.Site.Lng),
								datum.ObservationPoint.Point?.Name ?? datum.ObservationPoint.Site?.Prefefecture.GetLongName() + "/不明");
							RenderObjects.Add(render);
							RenderObjectMap.Add(datum.GetPointHash(), render);
						}
						var item = RenderObjectMap[datum.GetPointHash()];
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

				logger.Trace($"Time: {parseTime.TotalMilliseconds:.000},{(DateTime.Now - WorkStartedTime - parseTime).TotalMilliseconds:.000}");
			});
			monitorService.Start();

			aggregator.GetEvent<Events.UpdateFound>().Subscribe(b => UpdateAvailable = b);
			aggregator.GetEvent<Events.ShowSettingWindowRequested>().Subscribe(() => ShowSettingWindowCommand.Execute(null));

			Earthquakes.Add(new Earthquake
			{
				OccurrenceTime = DateTime.MinValue,
				Depth = 0,
				Intensity = JmaIntensity.Unknown,
				Magnitude = 0,
				Place = "受信中...",
			});

			aggregator.GetEvent<Events.EarthquakeUpdated>().Subscribe(e =>
			{
				Earthquakes.Clear();
				Earthquakes.AddRange(jmaXmlPullReceiver.Earthquakes);
				RaisePropertyChanged(nameof(FirstEarthquake));
				RaisePropertyChanged(nameof(SubEarthquakes));
			});
			jmaXmlPullReceiver.Initalize();

			Map = MessagePackSerializer.Deserialize<TopologyMap>(Resources.JapanMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
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
					Intensity = JmaIntensity.Error,
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

			var points = new List<LinkedRealTimeData>()
			{
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 0),
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 1),
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 2),
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 3),
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 4),
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 5),
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(), new ObservationPoint{ Region = "テスト", Name = "テスト" }), 6),
				new LinkedRealTimeData(new LinkedObservationPoint(new Site(){ PrefefectureId = 27 }, new ObservationPoint{ Region = "テスト", Name = "テスト" }), 7),
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
					Depth = 0,
					Place = "Warningテスト",
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

			Map = MessagePackSerializer.Deserialize<TopologyMap>(Resources.JapanMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			Zoom = 5;
			CenterLocation = new Location(36.474f, 135.264f);

			ConfirmedRenderObjects = new RenderObject[]
			{
				new EllipseRenderObject(new Location(34.6829f, 133.6015f), 500000, null, new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 160, 255)), 1)),
				new EllipseRenderObject(new Location(34.6829f, 133.6015f), 300000, new SolidColorBrush(Color.FromArgb(30, 255, 80, 120)), new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 80, 120)), 1)),
				new RawIntensityRenderObject(new Location(34.6829f, 135.6015f), "謎", 4),
				new EewCenterRenderObject(new Location(34.6829f, 133.6015f))
			};
		}
#endif
	}
}