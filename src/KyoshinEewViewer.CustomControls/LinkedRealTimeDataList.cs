using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.CustomControls
{
	public class LinkedRealtimeDataList : FrameworkElement
	{
		public IEnumerable<ImageAnalysisResult> Data
		{
			get => (IEnumerable<ImageAnalysisResult>)GetValue(DataProperty);
			set => SetValue(DataProperty, value);
		}
		public static readonly DependencyProperty DataProperty =
			DependencyProperty.Register("Data", typeof(IEnumerable<ImageAnalysisResult>), typeof(LinkedRealtimeDataList), new PropertyMetadata(new ImageAnalysisResult[]
			{
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.1, Color = System.Drawing.Color.FromArgb(255, 0, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.2, Color = System.Drawing.Color.FromArgb(255, 0, 255, 0) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.3, Color = System.Drawing.Color.FromArgb(255, 255, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.4, Color = System.Drawing.Color.FromArgb(255, 255, 255, 0) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.6, Color = System.Drawing.Color.FromArgb(255, 0, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.7, Color = System.Drawing.Color.FromArgb(255, 255, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.8, Color = System.Drawing.Color.FromArgb(255, 0, 0, 0) },
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 1.0, Color = System.Drawing.Color.FromArgb(255, 255, 0, 0) },
			}, (s, e) => (s as LinkedRealtimeDataList)?.InvalidateVisual()));

		public double ItemHeight
		{
			get => (double)GetValue(ItemHeightProperty);
			set => SetValue(ItemHeightProperty, value);
		}
		public static readonly DependencyProperty ItemHeightProperty =
			DependencyProperty.Register("ItemHeight", typeof(double), typeof(LinkedRealtimeDataList), new PropertyMetadata(24d, (s, e) => (s as LinkedRealtimeDataList)?.InvalidateVisual()));

		public double FirstItemHeight
		{
			get => (double)GetValue(FirstItemHeightProperty);
			set => SetValue(FirstItemHeightProperty, value);
		}
		public static readonly DependencyProperty FirstItemHeightProperty =
			DependencyProperty.Register("FirstItemHeight", typeof(double), typeof(LinkedRealtimeDataList), new PropertyMetadata(32d, (s, e) => (s as LinkedRealtimeDataList)?.InvalidateVisual()));

		public bool UseShindoIcon
		{
			get => (bool)GetValue(UseShindoIconProperty);
			set => SetValue(UseShindoIconProperty, value);
		}
		public static readonly DependencyProperty UseShindoIconProperty =
			DependencyProperty.Register("UseShindoIcon", typeof(bool), typeof(LinkedRealtimeDataList), new PropertyMetadata(true, (s, e) => (s as LinkedRealtimeDataList)?.InvalidateVisual()));


		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			drawingContext.DrawLinkedRealtimeData(Data, ItemHeight, FirstItemHeight, RenderSize.Width, RenderSize.Height, UseShindoIcon);
		}
	}
}
