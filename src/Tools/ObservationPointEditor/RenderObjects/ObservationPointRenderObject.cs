using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ObservationPointEditor.RenderObjects
{
	public class ObservationPointRenderObject : IRenderObject
	{
		private static Typeface TypeFace { get; } = new Typeface(new FontFamily("Yu Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
		private static Pen LinkedPen { get; }
		private static SolidColorBrush DisabledBrush { get; }

		static ObservationPointRenderObject()
		{
			LinkedPen = new Pen(Brushes.Lime, 2);
			LinkedPen.Freeze();

			DisabledBrush = new SolidColorBrush(Color.FromArgb(100, 127, 127, 127));
		}

		public ObservationPoint ObservationPoint { get; }
		public bool IsLinked { get; set; } = false;
		public bool IsSelected { get; set; } = false;
		public ObservationPointRenderObject(ObservationPoint point)
		{
			ObservationPoint = point;
		}
		public void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme)
		{
			var circleSize = (zoom - 4) * 1.75;
			var circleVector = new Vector(circleSize, circleSize);
			var pointCenter = ObservationPoint.Location.ToPixel(zoom);
			if (!bound.IntersectsWith(new Rect(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			var fillBrush = ObservationPoint.Type switch
			{
				ObservationPointType.KiK_net => Brushes.Red,
				ObservationPointType.K_NET => Brushes.Orange,
				_ => Brushes.DimGray,
			};
			if (ObservationPoint.IsSuspended)
				fillBrush = DisabledBrush;

			context.DrawRectangle(fillBrush, IsLinked && zoom >= 6 ? LinkedPen : null, new Rect(pointCenter - circleVector - (Vector)leftTopPixel, pointCenter + circleVector - (Vector)leftTopPixel));

			if (zoom >= 9 || IsSelected)
				context.DrawText(
					new FormattedText(ObservationPoint.Code, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TypeFace, 14, IsSelected ? Brushes.Crimson : Brushes.Black, 1),
					pointCenter - (Vector)leftTopPixel + new Vector(circleSize * 1.5, -8));
		}
	}
}
