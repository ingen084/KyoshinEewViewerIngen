using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using KyoshinMonitorLib;
using KyoshinMonitorLib.SkiaImages;
using SkiaSharp;
using System.Collections.Generic;

namespace KyoshinEewViewer.CustomControl;

public class LinkedRealtimeDataList : Control, ICustomDrawOperation
{
	private RealtimeDataRenderMode mode = RealtimeDataRenderMode.ShindoIcon;
	public static readonly DirectProperty<LinkedRealtimeDataList, RealtimeDataRenderMode> ModeProperty =
		AvaloniaProperty.RegisterDirect<LinkedRealtimeDataList, RealtimeDataRenderMode>(
			nameof(Mode),
			o => o.mode,
			(o, v) =>
			{
				o.mode = v;
				o.InvalidateVisual();
			});
	public RealtimeDataRenderMode Mode
	{
		get => mode;
		set => SetAndRaise(ModeProperty, ref mode, value);
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
				new ImageAnalysisResult(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.1, Color = new SKColor(255, 0, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.2, Color = new SKColor(0, 255, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.3, Color = new SKColor(255, 0, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.4, Color = new SKColor(255, 255, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.6, Color = new SKColor(0, 255, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.7, Color = new SKColor(255, 255, 255, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 0.8, Color = new SKColor(0, 0, 0, 255) },
				new ImageAnalysisResult(new ObservationPoint { Region = "テスト", Name = "テスト" }) { AnalysisResult = 1.0, Color = new SKColor(255, 0, 0, 255) },
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

		canvas.DrawLinkedRealtimeData(Data, ItemHeight, FirstItemHeight, (float)Bounds.Width, (float)Bounds.Height, Mode);

		canvas.Restore();
	}
	public override void Render(DrawingContext context) => context.Custom(this);


	public void Dispose()
	{
	}
}
