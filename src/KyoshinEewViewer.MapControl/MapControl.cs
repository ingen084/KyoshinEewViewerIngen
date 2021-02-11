using KyoshinEewViewer.MapControl.InternalControls;
using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace KyoshinEewViewer.MapControl
{
	public sealed class MapControl : Grid
	{
		#region MaxZoomLevel
		public static readonly DependencyProperty MaxZoomLevelProperty
			= DependencyProperty.Register("MaxZoomLevel", typeof(double), typeof(MapControl), new UIPropertyMetadata(20d));

		public double MaxZoomLevel
		{
			set
			{
				if (value < 0)
					return;
				SetValue(MaxZoomLevelProperty, value);
			}
			get => (double)GetValue(MaxZoomLevelProperty);
		}
		#endregion
		#region MinZoomLevel
		public static readonly DependencyProperty MinZoomLevelProperty
			= DependencyProperty.Register("MinZoomLevel", typeof(double), typeof(MapControl), new UIPropertyMetadata(0d));

		public double MinZoomLevel
		{
			set
			{
				if (value < 0)
					return;
				SetValue(MinZoomLevelProperty, value);
			}
			get => (double)GetValue(MinZoomLevelProperty);
		}
		#endregion
		#region ZoomProperty
		public static readonly DependencyProperty ZoomProperty
			= DependencyProperty.Register("Zoom", typeof(double), typeof(MapControl), new UIPropertyMetadata(5d, (s, e) =>
			{
				if (s is MapControl map)
				{
					var zoom = Math.Clamp((double)e.NewValue, map.MinZoomLevel, map.MaxZoomLevel);
					if (zoom != (double)e.NewValue)
					{
						map.Zoom = zoom;
						return;
					}
					if (map.LandLayer != null)
						map.LandLayer.Zoom = zoom;
					if (map.OverlayLayer != null)
						map.OverlayLayer.Zoom = zoom;
					if (map.RealtimeOverlayLayer != null)
						map.RealtimeOverlayLayer.Zoom = zoom;
					map.ApplySize();
					map.InvalidateChildVisual();
				}
			}));
		public double Zoom
		{
			set
			{
				if (value < 0 || value > MaxZoomLevel || value < MinZoomLevel)
					return;
				SetValue(ZoomProperty, value);
			}
			get => (double)GetValue(ZoomProperty);
		}
		#endregion
		#region CenterLocation
		public static readonly DependencyProperty CenterLocationProperty
			= DependencyProperty.Register("CenterLocation", typeof(Location), typeof(MapControl), new UIPropertyMetadata(new Location(0, 0), (s, e) =>
			{
				if (s is MapControl map)
				{
					if (map.CenterLocation != null)
					{
						var cl = map.CenterLocation;
						cl.Latitude = Math.Clamp(cl.Latitude, -80, 80);
						// 1回転させる
						if (cl.Longitude < -180)
							cl.Longitude += 360;
						if (cl.Longitude > 180)
							cl.Longitude -= 360;
						map.CenterLocation = cl;
					}

					map.ApplySize();
					map.InvalidateChildVisual();
				}
			}));
		public Location CenterLocation
		{
			set => SetValue(CenterLocationProperty, value);
			get => (Location)GetValue(CenterLocationProperty);
		}
		#endregion
		#region Padding
		public Thickness Padding
		{
			get => (Thickness)GetValue(PaddingProperty);
			set => SetValue(PaddingProperty, value);
		}
		public static readonly DependencyProperty PaddingProperty
			= DependencyProperty.Register("Padding", typeof(Thickness), typeof(MapControl), new PropertyMetadata(new Thickness(), (s, e) =>
			{
				if (s is MapControl map)
				{
					map.ApplySize();
					map.InvalidateChildVisual();
				}
			}));
		#endregion
		#region RenderObjects
		public IRenderObject[] RenderObjects
		{
			get => (IRenderObject[])GetValue(RenderObjectsProperty);
			set => SetValue(RenderObjectsProperty, value);
		}
		public static readonly DependencyProperty RenderObjectsProperty =
			DependencyProperty.Register("RenderObjects", typeof(IRenderObject[]), typeof(MapControl), new PropertyMetadata(null, (s, e) =>
			{
				if (s is MapControl map && map.OverlayLayer != null)
				{
					map.OverlayLayer.RenderObjects = (IRenderObject[])e.NewValue;
					map.OverlayLayer.InvalidateVisual();
				}
			}));
		public RealtimeRenderObject[] RealtimeRenderObjects
		{
			get => (RealtimeRenderObject[])GetValue(RealtimeRenderObjectsProperty);
			set => SetValue(RealtimeRenderObjectsProperty, value);
		}
		public static readonly DependencyProperty RealtimeRenderObjectsProperty =
			DependencyProperty.Register("RealtimeRenderObjects", typeof(RealtimeRenderObject[]), typeof(MapControl), new PropertyMetadata(null, (s, e) =>
			{
				if (s is MapControl map && map.OverlayLayer != null)
				{
					map.RealtimeOverlayLayer.RealtimeRenderObjects = (RealtimeRenderObject[])e.NewValue;
					map.RealtimeOverlayLayer.InvalidateVisual();
				}
			}));
		#endregion
		#region Map
		public Dictionary<LandLayerType, TopologyMap> Map
		{
			get => (Dictionary<LandLayerType, TopologyMap>)GetValue(MapProperty);
			set => SetValue(MapProperty, value);
		}
		public static readonly DependencyProperty MapProperty =
			DependencyProperty.Register("Map", typeof(Dictionary<LandLayerType, TopologyMap>), typeof(MapControl), new PropertyMetadata(null, async (s, e) =>
			{
				if (s is MapControl map && map.LandLayer != null)
				{
					await map.LandLayer.SetupMapAsync((Dictionary<LandLayerType, TopologyMap>)e.NewValue, (int)Math.Ceiling(map.MinZoomLevel), (int)Math.Ceiling(map.MaxZoomLevel));
					map.Dispatcher.Invoke(map.LandLayer.InvalidateVisual);
				}
			}));
		#endregion

		#region Animation
		private DoubleAnimation NavigateAnimation { get; } = new DoubleAnimation(0, 1, Duration.Automatic);
		private NagivateAnimationParameter AnimationParameter { get; set; }
		private static readonly DependencyProperty AnimationStepProperty =
			DependencyProperty.Register("AnimationStep", typeof(double), typeof(MapControl), new PropertyMetadata(0d, (s, e) =>
			{
				if (s is not MapControl map || map.AnimationParameter == null)
					return;
				var progress = (double)e.NewValue;
				var rawBoundPixel = new Rect(
					new Point(
						map.AnimationParameter.BaseRect.Left + Math.Cos(map.AnimationParameter.TopLeftTheta) * (map.AnimationParameter.TopLeftLength * progress),
						map.AnimationParameter.BaseRect.Top + Math.Sin(map.AnimationParameter.TopLeftTheta) * (map.AnimationParameter.TopLeftLength * progress)),
					new Point(
						map.AnimationParameter.BaseRect.Left + map.AnimationParameter.BaseRect.Width + Math.Cos(map.AnimationParameter.BottomRightTheta) * (map.AnimationParameter.BottomRightLength * progress),
						map.AnimationParameter.BaseRect.Top + map.AnimationParameter.BaseRect.Height + Math.Sin(map.AnimationParameter.BottomRightTheta) * (map.AnimationParameter.BottomRightLength * progress)));

				var boundPixel = new Rect(
					rawBoundPixel.TopLeft.ToLocation(map.Projection, map.AnimationParameter.BaseZoom).ToPixel(map.Projection, map.Zoom),
					rawBoundPixel.BottomRight.ToLocation(map.Projection, map.AnimationParameter.BaseZoom).ToPixel(map.Projection, map.Zoom));

				var relativeZoom = Math.Log(Math.Min(map.PaddedRect.Width / boundPixel.Width, map.PaddedRect.Height / boundPixel.Height), 2);
				map.CenterLocation = new Point(
					boundPixel.Left + boundPixel.Width / 2,
					boundPixel.Top + boundPixel.Height / 2).ToLocation(map.Projection, map.Zoom);
				map.Zoom += relativeZoom;
			}));

		public bool IsNavigating => AnimationParameter != null;
		#endregion Aniamation

		public MapProjection Projection { get; set; } = new MillerProjection();

		public void RefleshResourceCache()
		{
			LandLayer.RefleshResourceCache();
			InvalidateChildVisual();
		}

		// 指定した範囲をすべて表示できるように調整する
		public void Navigate(Rect bound, Duration duration)
		{
			var boundPixel = new Rect(bound.BottomLeft.CastLocation().ToPixel(Projection, Zoom), bound.TopRight.CastLocation().ToPixel(Projection, Zoom));
			var centerPixel = CenterLocation.ToPixel(Projection, Zoom);
			var halfRect = new Vector(PaddedRect.Width / 2, PaddedRect.Height / 2);
			var leftTop = centerPixel - halfRect;
			var rightBottom = centerPixel + halfRect;
			Navigate(new NagivateAnimationParameter(
					Zoom,
					new Rect(leftTop, rightBottom),
					boundPixel)
				, duration);
			//var scale = new Point(PaddedRect.Width / boundPixel.Width, PaddedRect.Height / boundPixel.Height);
			//var relativeZoom = Math.Log(Math.Min(scale.X, scale.Y), 2);
			//Navigate(relativeZoom,
			//	new Point(boundPixel.Left + boundPixel.Width / 2, boundPixel.Top + boundPixel.Height / 2).ToLocation(Zoom),
			//	duration, true);
		}
		internal void Navigate(NagivateAnimationParameter parameter, Duration dulation)
		{
			if (AnimationParameter != null || NavigateAnimation.BeginTime != null)
			{
				// アニメーションを止める
				NavigateAnimation.BeginTime = null;
				AnimationParameter = null;
			}

			//if (relativeZoom)
			//	zoom += Zoom;
			//zoom = Math.Max(Math.Min(zoom, MaxZoomLevel), MinZoomLevel);
			//zoom -= zoom % .25; // .25単位に丸めておく

			// 時間がゼロなら強制リセット
			if (dulation.TimeSpan <= TimeSpan.Zero)
			{
				// todo: relativeな座標を計算する
				//if (parameter.RelativeMode)
				//{
				//	
				//	// CenterLocation = ;
				//	Zoom = parameter.ToZoom;
				//	return;
				//}
				var boundPixel = new Rect(parameter.ToRect.TopLeft, parameter.ToRect.BottomRight);
				var scale = new Point(PaddedRect.Width / boundPixel.Width, PaddedRect.Height / boundPixel.Height);
				var relativeZoom = Math.Log(Math.Min(scale.X, scale.Y), 2);
				CenterLocation = new Point(
					boundPixel.Left + boundPixel.Width / 2,
					boundPixel.Top + boundPixel.Height / 2).ToLocation(Projection, Zoom);
				Zoom += relativeZoom;
				return;
			}

			AnimationParameter = parameter;
			NavigateAnimation.BeginTime = TimeSpan.Zero;
			NavigateAnimation.Duration = dulation;
			BeginAnimation(AnimationStepProperty, NavigateAnimation);
			return;
		}


		public Rect PaddedRect { get; private set; }

		static MapControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(MapControl), new FrameworkPropertyMetadata(typeof(MapControl)));
		}

		private LandLayer LandLayer { get; set; }
		private OverlayLayer OverlayLayer { get; set; }
		private RealtimeOverlayLayer RealtimeOverlayLayer { get; set; }
		protected override void OnInitialized(EventArgs e)
		{
			Children.Add(LandLayer = new LandLayer
			{
				Projection = Projection,
				Zoom = Zoom,
				CenterLocation = CenterLocation,
			});
			LandLayer.RefleshResourceCache();
			if (Map != null)
			{
				var map = Map;
				var minZoom = MinZoomLevel;
				var maxZoom = MaxZoomLevel;
				Task.Run(async () =>
				{
					await LandLayer.SetupMapAsync(map, (int)Math.Ceiling(minZoom), (int)Math.Ceiling(maxZoom));
					Dispatcher.Invoke(LandLayer.InvalidateVisual);
				});
			}
			Children.Add(OverlayLayer = new OverlayLayer
			{
				Projection = Projection,
				Zoom = Zoom,
				CenterLocation = CenterLocation,
				RenderObjects = RenderObjects,
			});
			Children.Add(RealtimeOverlayLayer = new RealtimeOverlayLayer
			{
				Projection = Projection,
				Zoom = Zoom,
				CenterLocation = CenterLocation,
				RealtimeRenderObjects = RealtimeRenderObjects,
			});
			ApplySize();

			NavigateAnimation.EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut };
			NavigateAnimation.Completed += (s, e) =>
			{
				AnimationParameter = null;
				NavigateAnimation.BeginTime = null;
			};

			base.OnInitialized(e);
		}
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			ApplySize();
			base.OnRenderSizeChanged(sizeInfo);
			InvalidateChildVisual();
		}
		private void ApplySize()
		{
			// DP Cache
			var renderSize = RenderSize;
			var padding = Padding;
			PaddedRect = new Rect(new Point(padding.Left, padding.Top), new Point(Math.Max(0, renderSize.Width - padding.Right), Math.Max(0, renderSize.Height - padding.Bottom)));
			var zoom = Zoom;
			var centerLocation = CenterLocation ?? new Location(0, 0); // null island

			var halfRenderSize = new Vector(PaddedRect.Width / 2, PaddedRect.Height / 2);
			// 左上/右下のピクセル座標
			var leftTop = centerLocation.ToPixel(Projection, zoom) - halfRenderSize - new Vector(padding.Left, padding.Top);
			var rightBottom = centerLocation.ToPixel(Projection, zoom) + halfRenderSize + new Vector(padding.Right, padding.Bottom);

			if (LandLayer != null)
			{
				LandLayer.LeftTopLocation = leftTop.ToLocation(Projection, zoom).CastPoint();
				LandLayer.ViewAreaRect = new Rect(LandLayer.LeftTopLocation, rightBottom.ToLocation(Projection, zoom).CastPoint());
			}
			if (OverlayLayer != null)
			{
				OverlayLayer.LeftTopPixel = leftTop;
				OverlayLayer.PixelBound = new Rect(leftTop, rightBottom);
			}
			if (RealtimeOverlayLayer != null)
			{
				RealtimeOverlayLayer.LeftTopPixel = leftTop;
				RealtimeOverlayLayer.PixelBound = new Rect(leftTop, rightBottom);
			}
		}

		public void InvalidateChildVisual()
		{
			LandLayer?.InvalidateVisual();
			OverlayLayer?.InvalidateVisual();
			RealtimeOverlayLayer?.InvalidateVisual();
		}
	}
}
