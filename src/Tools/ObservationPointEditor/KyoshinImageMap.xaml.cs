using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ObservationPointEditor
{
	/// <summary>
	/// KyoshinImageMap.xaml の相互作用ロジック
	/// </summary>
	public partial class KyoshinImageMap : UserControl
	{
		static Rect BaseRect = new Rect(new Size(352, 400));

		public Point CenterPoint
		{
			get => (Point)GetValue(CenterPointProperty);
			set => SetValue(CenterPointProperty, value);
		}
		public static readonly DependencyProperty CenterPointProperty =
			DependencyProperty.Register("CenterPoint", typeof(Point), typeof(KyoshinImageMap), new PropertyMetadata(new Point(190, 200), (s, e) =>
			{
				if (s is KyoshinImageMap map)
					map.InvalidateVisual();
			}));

		public double Scale
		{
			get => (double)GetValue(ScaleProperty);
			set => SetValue(ScaleProperty, value);
		}
		public static readonly DependencyProperty ScaleProperty =
			DependencyProperty.Register("Scale", typeof(double), typeof(KyoshinImageMap), new PropertyMetadata(1.0, (s, e) =>
			{
				if (s is KyoshinImageMap map)
					map.InvalidateVisual();
			}));


		private ObservationPoint[] _points;
		public ObservationPoint[] Points
		{
			get => _points;
			set
			{
				_points = value;
				InvalidateVisual();
			}
		}
		private ObservationPoint _selectedPoint;
		public ObservationPoint SelectedPoint
		{
			get => _selectedPoint;
			set
			{
				_selectedPoint = value;
				InvalidateVisual();
			}
		}

		public event Action<(MouseButton button, Point2 location)> PointClicked;
		ImageSource BackgroundImage { get; }
		ImageSource KyoshinImage { get; set; }
		Pen SelectedBorderPen { get; }
		public KyoshinImageMap()
		{
			InitializeComponent();
			slider.ValueChanged += (s, e) => Scale = slider.Value;
			RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);

			SelectedBorderPen = new Pen(Brushes.Magenta, 2);
			SelectedBorderPen.Freeze();

			BackgroundImage = new BitmapImage(new Uri("http://www.kmoni.bosai.go.jp/data/map_img/CommonImg/base_map_w.gif"));
			foreach (var v in Enum.GetValues<RealtimeDataType>())
				imageTypeCombobox.Items.Add(v);
			imageTypeCombobox.SelectedItem = RealtimeDataType.Shindo;
			UpdateKyoshinImage();
			refleshImageButton.Click += (s, e) => UpdateKyoshinImage();
			imageTypeCombobox.SelectionChanged += (s, e) => UpdateKyoshinImage();

			showMonitorImageCheckBox.Click += (s, e) => InvalidateVisual();
			showObservationPointCheckBox.Click += (s, e) => InvalidateVisual();

			Point? MouseDownPoint = null;
			Point? PreviousMousePoint = null;
			hitGrid.MouseDown += (s, e) =>
			{
				//if (e.LeftButton == MouseButtonState.Pressed)
				PreviousMousePoint = MouseDownPoint = e.GetPosition(hitGrid);
			};
			hitGrid.MouseMove += (s, e) =>
			{
				if (e.LeftButton == MouseButtonState.Pressed && PreviousMousePoint is Point point)
				{
					var current = e.GetPosition(hitGrid);
					var nextCenterPoint = CenterPoint + (point - current) / Scale;
					if (!BaseRect.Contains(nextCenterPoint))
					{
						nextCenterPoint.X = Math.Max(0, Math.Min(BaseRect.Width, nextCenterPoint.X));
						nextCenterPoint.Y = Math.Max(0, Math.Min(BaseRect.Height, nextCenterPoint.Y));
					}
					CenterPoint = nextCenterPoint;
					PreviousMousePoint = current;
				}
			};
			hitGrid.MouseUp += (s, e) =>
			{
				var pos = e.GetPosition(hitGrid);
				if (MouseDownPoint is not Point cp || cp != pos)
					return;

				var scale = Scale;

				var halfRenderSize = new Vector(RenderSize.Width / 2, RenderSize.Height / 2);
				var offset = ((Vector)CenterPoint * scale) - halfRenderSize;

				var rpos = (Vector)(pos + offset) / scale;
				var rpos2 = new Point2((int)Math.Floor(rpos.X), (int)Math.Floor(rpos.Y));

				PointClicked?.Invoke((e.ChangedButton, rpos2));
			};
			hitGrid.MouseWheel += (s, e) =>
			{
				slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, slider.Value + e.Delta / 120 * slider.TickFrequency));
			};
		}

		private void UpdateKyoshinImage()
		{
			KyoshinImage = new BitmapImage(new Uri(WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, DateTime.Now.AddMinutes(-1), (RealtimeDataType)imageTypeCombobox.SelectedItem)));
			InvalidateVisual();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			var scale = Scale;

			var halfRenderSize = new Vector(RenderSize.Width / 2, RenderSize.Height / 2);
			var offset = (Point)((Vector)CenterPoint * -scale) + halfRenderSize;

			// モニタ画像描画
			drawingContext.DrawImage(BackgroundImage, new Rect(offset, new Size(BackgroundImage.Width * scale, BackgroundImage.Height * scale)));
			if (KyoshinImage != null && showMonitorImageCheckBox.IsChecked == true)
				drawingContext.DrawImage(KyoshinImage, new Rect(offset, new Size(BackgroundImage.Width * scale, BackgroundImage.Height * scale)));

			// 観測点描画
			if (!Points?.Any() ?? true)
				return;
			if (showObservationPointCheckBox.IsChecked != true)
				return;

			var displayRect = new Rect(CenterPoint - halfRenderSize / scale, CenterPoint + halfRenderSize / scale);
			foreach (var point in Points)
			{
				if (point.Point is not Point2 p || !displayRect.Contains(p.X, p.Y))
					continue;

				var fillBrush = point.Type switch
				{
					ObservationPointType.KiK_net => Brushes.Red,
					ObservationPointType.K_NET => Brushes.Orange,
					_ => Brushes.DimGray,
				};
				if (point.IsSuspended)
					fillBrush = Brushes.Gray;

				drawingContext.DrawRectangle(fillBrush, point == SelectedPoint ? SelectedBorderPen : null, new Rect(new Vector(p.X, p.Y) * scale + offset, new Size(scale, scale)));
			}
		}
	}
}
