using KyoshinMonitorLib;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.CustomControls
{
	public class LinkedRealTimeDataList : FrameworkElement
	{
		public IEnumerable<LinkedRealtimeData> Data
		{
			get => (IEnumerable<LinkedRealtimeData>)GetValue(DataProperty);
			set => SetValue(DataProperty, value);
		}
		public static readonly DependencyProperty DataProperty =
			DependencyProperty.Register("Data", typeof(IEnumerable<LinkedRealtimeData>), typeof(LinkedRealTimeDataList), new PropertyMetadata(new LinkedRealtimeData[]
			{
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "テスト", Name = "テスト" }), -0.3f),
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "テスト", Name = "テスト" }), 1),
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "テスト", Name = "テスト" }), 2),
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "テスト", Name = "テスト" }), 3),
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "これはとても長い", Name = "テスト" }), 4),
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "テスト", Name = "テスト" }), 5),
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "テスト", Name = "テスト" }), 6),
				new LinkedRealtimeData(new LinkedObservationPoint(new KyoshinMonitorLib.ApiResult.AppApi.Site(), new ObservationPoint(){ Region = "テスト", Name = "テスト" }), 7),
			}, (s, e) => (s as LinkedRealTimeDataList)?.InvalidateVisual()));

		public double ItemHeight
		{
			get => (double)GetValue(ItemHeightProperty);
			set => SetValue(ItemHeightProperty, value);
		}
		public static readonly DependencyProperty ItemHeightProperty =
			DependencyProperty.Register("ItemHeight", typeof(double), typeof(LinkedRealTimeDataList), new PropertyMetadata(24d, (s, e) => (s as LinkedRealTimeDataList)?.InvalidateVisual()));

		public double FirstItemHeight
		{
			get => (double)GetValue(FirstItemHeightProperty);
			set => SetValue(FirstItemHeightProperty, value);
		}
		public static readonly DependencyProperty FirstItemHeightProperty =
			DependencyProperty.Register("FirstItemHeight", typeof(double), typeof(LinkedRealTimeDataList), new PropertyMetadata(32d, (s, e) => (s as LinkedRealTimeDataList)?.InvalidateVisual()));

		public bool UseShindoIcon
		{
			get => (bool)GetValue(UseShindoIconProperty);
			set => SetValue(UseShindoIconProperty, value);
		}
		public static readonly DependencyProperty UseShindoIconProperty =
			DependencyProperty.Register("UseShindoIcon", typeof(bool), typeof(LinkedRealTimeDataList), new PropertyMetadata(true, (s, e) => (s as LinkedRealTimeDataList)?.InvalidateVisual()));


		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			drawingContext.DrawLinkedRealTimeData(Data, ItemHeight, FirstItemHeight, RenderSize.Width, RenderSize.Height, UseShindoIcon);
		}
	}
}
