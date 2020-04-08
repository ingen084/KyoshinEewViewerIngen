using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ObservationPointEditor
{
	/// <summary>
	/// ObservationPointGridView.xaml の相互作用ロジック
	/// </summary>
	public partial class ObservationPointGridView : UserControl
	{
		private ObservationPoint[] _points;
		public ObservationPoint[] Points
		{
			get => _points;
			set
			{
				_points = value;
				Dispatcher.Invoke(() =>
				{
					dummyContent.Height = (_points?.Length - 1) * 20 ?? 0;
					searchTextBox.Text = "";
					SelectedPoint = null;
					FilterdObservationPoints = Points;
					InvalidateVisual();
				});
			}
		}
		private IEnumerable<ObservationPoint> FilterdObservationPoints { get; set; }

		public event Action<(ObservationPoint oldValue, ObservationPoint newValue)> SelectedPointChanged;
		private ObservationPoint _selectedPoint;
		public ObservationPoint SelectedPoint
		{
			get => _selectedPoint;
			set
			{
				var old = _selectedPoint;
				if (_selectedPoint != value)
					SelectedPointChanged?.Invoke((old, value));
				_selectedPoint = value;
				if (_selectedPoint != null)
				{
					var ni = FilterdObservationPoints.Select((p, i) => new { IsMatch = p == _selectedPoint, Index = i }).FirstOrDefault(d => d.IsMatch)?.Index;
					if (ni is int index)
					{
						var indexOffset = index * 20;

						if (indexOffset < scrollViewer.VerticalOffset || indexOffset > scrollViewer.VerticalOffset + RenderSize.Height - 20)
							scrollViewer.ScrollToVerticalOffset(indexOffset);
					}
				}
				InvalidateVisual();
			}
		}

		static Typeface Typeface = new Typeface("Yu Gothic UI");
		static Typeface MonoTypeface = new Typeface("Consolas");

		Pen SubBorderPen;
		Brush SelectedBackgroundBrush;
		Brush DisabledItemForegroundBrush;
		public ObservationPointGridView()
		{
			InitializeComponent();
			SubBorderPen = new Pen(Brushes.Gray, 1);
			SubBorderPen.Freeze();
			SelectedBackgroundBrush = new SolidColorBrush(Color.FromArgb(100, 25, 25, 112));
			SelectedBackgroundBrush.Freeze();
			DisabledItemForegroundBrush = new SolidColorBrush(Colors.DarkGray);
			DisabledItemForegroundBrush.Freeze();

			scrollViewer.ScrollChanged += (s, e) => InvalidateVisual();
			searchTextBox.TextChanged += (s, e) =>
			{
				if (!string.IsNullOrWhiteSpace(searchTextBox.Text))
					FilterdObservationPoints = Points?.Where(p => p.Code.Contains(searchTextBox.Text.ToUpper()));
				dummyContent.Height = Math.Max(0, FilterdObservationPoints?.Count() * 20 ?? 0);
				InvalidateVisual();
			};
		}

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			base.OnMouseDown(e);

			if (!FilterdObservationPoints?.Any() ?? true)
			{
				SelectedPoint = null;
				InvalidateVisual();
				return;
			}

			var pos = e.GetPosition(this);
			var topIndex = (int)Math.Floor(scrollViewer.VerticalOffset / 20);
			var topOffset = -scrollViewer.VerticalOffset % 20 + 40;

			var clickedIndex = (int)((pos.Y - topOffset) / 20 + topIndex);
			if (clickedIndex >= FilterdObservationPoints.Count())
			{
				SelectedPoint = null;
				InvalidateVisual();
				return;
			}
			SelectedPoint = FilterdObservationPoints.Skip(clickedIndex).FirstOrDefault();
			InvalidateVisual();
		}

		public event Action ItemDoubleClicked;
		protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
		{
			base.OnMouseDoubleClick(e);

			if (!FilterdObservationPoints?.Any() ?? true)
			{
				SelectedPoint = null;
				InvalidateVisual();
				return;
			}

			var pos = e.GetPosition(this);
			var topIndex = (int)Math.Floor(scrollViewer.VerticalOffset / 20);
			var topOffset = -scrollViewer.VerticalOffset % 20 + 40;

			var clickedIndex = (int)((pos.Y - topOffset) / 20 + topIndex);
			if (clickedIndex >= FilterdObservationPoints.Count())
			{
				SelectedPoint = null;
				InvalidateVisual();
				return;
			}
			SelectedPoint = FilterdObservationPoints.Skip(clickedIndex).FirstOrDefault();
			InvalidateVisual();
			ItemDoubleClicked?.Invoke();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(0, 0), RenderSize));

			if (FilterdObservationPoints?.Any() ?? false)
			{
				var length = FilterdObservationPoints.Count();

				var topIndex = (int)Math.Floor(scrollViewer.VerticalOffset / 20);
				var topOffset = -scrollViewer.VerticalOffset % 20 + 40;
				var renderCount = (int)Math.Floor((RenderSize.Height - topOffset) / 20 + 1);


				foreach (var point in FilterdObservationPoints.Skip(topIndex).Take(Math.Min(length - topIndex, renderCount)))
				{
					RenderItem(drawingContext, point, topOffset);
					topOffset += 20;
				}
			}
			RenderHeader(drawingContext);
		}

		private const int ItemsMargin = 5;
		private static (string name, int width)[] Headers = new (string name, int width)[]
		{
			(" 種", 23),
			("ID", 47),
			("観測点名", 70),
			("広域名", 100),
			("状況", -1),
		};
		private void RenderHeader(DrawingContext drawingContext)
		{
			drawingContext.DrawRectangle(Brushes.LightGray, null, new Rect(new Point(0, 0), new Point(RenderSize.Width, 40)));

			int current = 0;
			foreach ((string name, int width) in Headers)
			{
				drawingContext.DrawText(new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface, FontSize, Foreground, 1), new Point(current + ItemsMargin, 20));
				if (width <= 0)
					break;
				current += width + ItemsMargin * 2;
				drawingContext.DrawLine(SubBorderPen, new Point(current, 20), new Point(current, RenderSize.Height));
			}
			drawingContext.DrawLine(new Pen(Foreground, 1), new Point(0, 40), new Point(RenderSize.Width, 40));
		}
		private void RenderItem(DrawingContext drawingContext, ObservationPoint point, double topMargin)
		{
			int current = 0;
			if (point == SelectedPoint)
				drawingContext.DrawRectangle(SelectedBackgroundBrush, null, new Rect(new Point(0, topMargin), new Point(RenderSize.Width, topMargin + 20)));

			foreach ((string name, int width) in Headers)
			{
				switch (name)
				{
					case "ID":
						drawingContext.DrawText(new FormattedText(point.Code, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, MonoTypeface, FontSize, point.IsSuspended ? DisabledItemForegroundBrush : Foreground, 1), new Point(current + ItemsMargin, topMargin));
						break;
					case " 種":
						{
							Brush textColor = Brushes.Gray;
							string text = "不明";
							switch (point.Type)
							{
								case ObservationPointType.KiK_net:
									textColor = Brushes.Red;
									text = "KiK";
									break;
								case ObservationPointType.K_NET:
									textColor = Brushes.Orange;
									text = " K";
									break;
							}
							drawingContext.DrawText(new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, MonoTypeface, FontSize, textColor, 1), new Point(current + ItemsMargin, topMargin));
							break;
						}
					case "観測点名":
						drawingContext.DrawText(new FormattedText(point.Name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface, FontSize, point.IsSuspended ? DisabledItemForegroundBrush : Foreground, 1), new Point(current + ItemsMargin, topMargin));
						break;
					case "広域名":
						drawingContext.DrawText(new FormattedText(point.Region, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface, FontSize, point.IsSuspended ? DisabledItemForegroundBrush : Foreground, 1), new Point(current + ItemsMargin, topMargin));
						break;
					case "状況":
						if (point.Point == null)
							drawingContext.DrawText(new FormattedText("地点未設定", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface, FontSize, point.IsSuspended ? DisabledItemForegroundBrush : Foreground, 1), new Point(current + ItemsMargin, topMargin));
						break;
				}
				if (width <= 0)
					break;
				current += width + ItemsMargin * 2;
			}
			drawingContext.DrawLine(SubBorderPen, new Point(0, topMargin + 20), new Point(RenderSize.Width, topMargin + 20));
		}
	}
}
