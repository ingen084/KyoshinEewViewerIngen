using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.CustomControl
{
	public class LinkedRealtimeDataList : Control, ICustomDrawOperation
	{
		private bool useShindoIcon = true;
		public static readonly DirectProperty<LinkedRealtimeDataList, bool> UseShindoIconProperty =
			AvaloniaProperty.RegisterDirect<LinkedRealtimeDataList, bool>(
				nameof(UseShindoIcon),
				o => o.useShindoIcon,
				(o, v) =>
				{
					o.useShindoIcon = v;
					o.InvalidateVisual();
				});
		public bool UseShindoIcon
		{
			get => useShindoIcon;
			set => SetAndRaise(UseShindoIconProperty, ref useShindoIcon, value);
		}

		private float itemHeight = 24;
		public static readonly DirectProperty<LinkedRealtimeDataList, float> ItemHeightProperty =
			AvaloniaProperty.RegisterDirect<LinkedRealtimeDataList, float>(
				nameof(ItemHeight),
				o => o.ItemHeight,
				(o, v) =>
				{
					o.itemHeight = v;
					o.InvalidateVisual();
				});
		public float ItemHeight
		{
			get => itemHeight;
			set => SetAndRaise(ItemHeightProperty, ref itemHeight, value);
		}

		private float firstItemHeight = 24;
		public static readonly DirectProperty<LinkedRealtimeDataList, float> FirstItemHeightProperty =
			AvaloniaProperty.RegisterDirect<LinkedRealtimeDataList, float>(
				nameof(FirstItemHeight),
				o => o.FirstItemHeight,
				(o, v) =>
				{
					o.firstItemHeight = v;
					o.InvalidateVisual();
				});
		public float FirstItemHeight
		{
			get => firstItemHeight;
			set => SetAndRaise(ItemHeightProperty, ref firstItemHeight, value);
		}

		private IEnumerable<ImageAnalysisResult>? data = new[]
			{
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.1, Color = System.Drawing.Color.FromArgb(255, 0, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.2, Color = System.Drawing.Color.FromArgb(255, 0, 255, 0) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.3, Color = System.Drawing.Color.FromArgb(255, 255, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.4, Color = System.Drawing.Color.FromArgb(255, 255, 255, 0) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.6, Color = System.Drawing.Color.FromArgb(255, 0, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.7, Color = System.Drawing.Color.FromArgb(255, 255, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.8, Color = System.Drawing.Color.FromArgb(255, 0, 0, 0) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 1.0, Color = System.Drawing.Color.FromArgb(255, 255, 0, 0) },
			};
		public static readonly DirectProperty<LinkedRealtimeDataList, IEnumerable<ImageAnalysisResult>?> DataProperty =
					AvaloniaProperty.RegisterDirect<LinkedRealtimeDataList, IEnumerable<ImageAnalysisResult>?>(
						nameof(Data),
						o => o.data,
						(o, v) =>
						{
							o.data = v;
							o.InvalidateVisual();
						});
		public IEnumerable<ImageAnalysisResult>? Data
		{
			get => data;
			set => SetAndRaise(DataProperty, ref data, value);
		}

		public bool Equals(ICustomDrawOperation? other) => false;
		public bool HitTest(Point p) => false;

		public void Render(IDrawingContextImpl context)
		{
			var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
			if (canvas == null)
				return;
			canvas.Save();

			canvas.DrawLinkedRealtimeData(Data, ItemHeight, FirstItemHeight, (float)Bounds.Width, (float)Bounds.Height, UseShindoIcon);

			canvas.Restore();
		}
		public override void Render(DrawingContext context) => context.Custom(this);


		public void Dispose()
		{
		}
	}
}
