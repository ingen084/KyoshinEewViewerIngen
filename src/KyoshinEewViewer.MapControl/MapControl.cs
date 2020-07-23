using KyoshinEewViewer.MapControl.InternalControls;
using KyoshinMonitorLib;
using System;
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
			= DependencyProperty.Register("Zoom", typeof(double), typeof(MapControl), new UIPropertyMetadata(0d, (s, e) =>
			{
				if (s is MapControl map)
				{
					if (map.LandRender != null)
						map.LandRender.Zoom = (double)e.NewValue;
					if (map.OverlayRender != null)
						map.OverlayRender.Zoom = (double)e.NewValue;
					if (map.RealtimeOverlayRender != null)
						map.RealtimeOverlayRender.Zoom = (double)e.NewValue;
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
						cl.Latitude = Math.Min(Math.Max(cl.Latitude, -80), 80);
						cl.Longitude = Math.Min(Math.Max(cl.Longitude, -180), 180);
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
				if (s is MapControl map && map.OverlayRender != null)
				{
					map.OverlayRender.RenderObjects = (IRenderObject[])e.NewValue;
					map.OverlayRender.InvalidateVisual();
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
				if (s is MapControl map && map.OverlayRender != null)
				{
					map.RealtimeOverlayRender.RealtimeRenderObjects = (RealtimeRenderObject[])e.NewValue;
					map.RealtimeOverlayRender.InvalidateVisual();
				}
			}));
		#endregion
		#region Map
		public TopologyMap Map
		{
			get => (TopologyMap)GetValue(MapProperty);
			set => SetValue(MapProperty, value);
		}
		public static readonly DependencyProperty MapProperty =
			DependencyProperty.Register("Map", typeof(TopologyMap), typeof(MapControl), new PropertyMetadata(null, (s, e) =>
			{
				if (s is MapControl map && map.LandRender != null)
				{
					map.LandRender.Controller = new FeatureCacheController((TopologyMap)e.NewValue);
					map.LandRender.InvalidateVisual();
				}
			}));
		#endregion

		private DoubleAnimation NavigateAnimation { get; } = new DoubleAnimation(0, 1, Duration.Automatic);
		private NagivateAnimationParameter AnimationParameter { get; set; }
		private static readonly DependencyProperty AnimationStepProperty =
			DependencyProperty.Register("AnimationStep", typeof(double), typeof(MapControl), new PropertyMetadata(0d, (s, e) =>
			{
				if (!(s is MapControl map) || map.AnimationParameter == null)
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
					rawBoundPixel.TopLeft.ToLocation(map.AnimationParameter.BaseZoom).ToPixel(map.Zoom),
					rawBoundPixel.BottomRight.ToLocation(map.AnimationParameter.BaseZoom).ToPixel(map.Zoom));

				var relativeZoom = Math.Log(Math.Min(map.PaddedRect.Width / boundPixel.Width, map.PaddedRect.Height / boundPixel.Height), 2);
				map.CenterLocation = new Point(
					boundPixel.Left + boundPixel.Width / 2,
					boundPixel.Top + boundPixel.Height / 2).ToLocation(map.Zoom);
				map.Zoom += relativeZoom;
			}));

		public bool IsNavigating => AnimationParameter != null;

		// 指定した範囲をすべて表示できるように調整する
		public void Navigate(Rect bound, Duration duration)
		{
			var boundPixel = new Rect(bound.BottomLeft.AsLocation().ToPixel(Zoom), bound.TopRight.AsLocation().ToPixel(Zoom));
			var centerPixel = CenterLocation.ToPixel(Zoom);
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
				//if (parameter.RelativeMode)
				//{
				//	// todo: relativeな座標を計算する
				//	// CenterLocation = ;
				//	Zoom = parameter.ToZoom;
				//	return;
				//}
				var boundPixel = new Rect(parameter.ToRect.TopLeft, parameter.ToRect.BottomRight);
				var scale = new Point(PaddedRect.Width / boundPixel.Width, PaddedRect.Height / boundPixel.Height);
				var relativeZoom = Math.Log(Math.Min(scale.X, scale.Y), 2);
				CenterLocation = new Point(
					boundPixel.Left + boundPixel.Width / 2,
					boundPixel.Top + boundPixel.Height / 2).ToLocation(Zoom);
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

		private LandLayer LandRender { get; set; }
		private OverlayLayer OverlayRender { get; set; }
		private RealtimeOverlayLayer RealtimeOverlayRender { get; set; }
		protected override void OnInitialized(EventArgs e)
		{
			Children.Add(LandRender = new LandLayer
			{
				Zoom = Zoom,
				CenterLocation = CenterLocation,
			});
			if (Map != null)
				LandRender.Controller = new FeatureCacheController(Map);
			Children.Add(OverlayRender = new OverlayLayer
			{
				Zoom = Zoom,
				CenterLocation = CenterLocation,
				RenderObjects = RenderObjects,
			});
			Children.Add(RealtimeOverlayRender = new RealtimeOverlayLayer
			{
				Zoom = Zoom,
				CenterLocation = CenterLocation,
				RealtimeRenderObjects = RealtimeRenderObjects,
			});
			ApplySize();

			NavigateAnimation.EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut };
			NavigateAnimation.Completed += (s, e) =>
			{
				//SystemSounds.Beep.Play();
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
			var leftTop = centerLocation.ToPixel(zoom) - halfRenderSize - new Vector(padding.Left, padding.Top);
			var rightBottom = centerLocation.ToPixel(zoom) + halfRenderSize + new Vector(padding.Right, padding.Bottom);

			if (LandRender != null)
			{
				LandRender.LeftTopLocation = leftTop.ToLocation(zoom).AsPoint();
				LandRender.ViewAreaRect = new Rect(LandRender.LeftTopLocation, rightBottom.ToLocation(zoom).AsPoint());
			}
			if (OverlayRender != null)
			{
				OverlayRender.LeftTopPixel = leftTop;
				OverlayRender.PixelBound = new Rect(leftTop, rightBottom);
			}
			if (RealtimeOverlayRender != null)
			{
				RealtimeOverlayRender.LeftTopPixel = leftTop;
				RealtimeOverlayRender.PixelBound = new Rect(leftTop, rightBottom);
			}
		}

		public void InvalidateChildVisual()
		{
			LandRender?.InvalidateVisual();
			OverlayRender?.InvalidateVisual();
			RealtimeOverlayRender?.InvalidateVisual();
		}
	}
}
