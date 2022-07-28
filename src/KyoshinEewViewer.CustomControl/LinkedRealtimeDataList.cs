using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinMonitorLib;
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

	private IEnumerable<RealtimeObservationPoint>? data = new[]
	{
		new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.0, LatestColor = new SKColor(255, 0, 0, 255) },
		new RealtimeObservationPoint(new ObservationPoint { Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.5, LatestColor = new SKColor(0, 255, 0, 255) },
		new RealtimeObservationPoint(new ObservationPoint { Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 1.5, LatestColor = new SKColor(255, 0, 255, 255) },
		new RealtimeObservationPoint(new ObservationPoint { Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 2.5, LatestColor = new SKColor(255, 255, 0, 255) },
		new RealtimeObservationPoint(new ObservationPoint { Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 3.5, LatestColor = new SKColor(0, 255, 255, 255) },
		new RealtimeObservationPoint(new ObservationPoint { Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 4.5, LatestColor = new SKColor(255, 255, 255, 255) },
		new RealtimeObservationPoint(new ObservationPoint { Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 5.8, LatestColor = new SKColor(0, 0, 0, 255) },
		new RealtimeObservationPoint(new ObservationPoint { Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 7.0, LatestColor = new SKColor(255, 0, 0, 255) },
	};
	public static readonly DirectProperty<LinkedRealtimeDataList, IEnumerable<RealtimeObservationPoint>?> DataProperty =
		AvaloniaProperty.RegisterDirect<LinkedRealtimeDataList, IEnumerable<RealtimeObservationPoint>?>(
			nameof(Data),
			o => o.data,
			(o, v) =>
			{
				o.data = v;
				o.InvalidateVisual();
			});
	public IEnumerable<RealtimeObservationPoint>? Data
	{
		get => data;
		set => SetAndRaise(DataProperty, ref data, value);
	}

	protected override void OnInitialized()
	{
		base.OnInitialized();

		if (!FixedObjectRenderer.PaintCacheInitalized)
			FixedObjectRenderer.UpdateIntensityPaintCache(this);
	}

	public bool Equals(ICustomDrawOperation? other) => false;
	public bool HitTest(Point p) => false;

	public void Render(IDrawingContextImpl context)
	{
		var canvas = context.TryGetSkiaDrawingContext()?.SkCanvas;
		if (canvas == null)
			return;
		canvas.Save();

		canvas.DrawLinkedRealtimeData(Data, ItemHeight, (float)Bounds.Width, (float)Bounds.Height, Mode);

		canvas.Restore();
	}
	public override void Render(DrawingContext context) => context.Custom(this);


	public void Dispose()
	{
	}
}
