using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.MapEdit.Controls;

public class KmoniImageView : Avalonia.Controls.Control, ICustomDrawOperation
{
	public static readonly DirectProperty<KmoniImageView, IEnumerable<ObservationPoint>?> ObservationPointsProperty =
	AvaloniaProperty.RegisterDirect<KmoniImageView, IEnumerable<ObservationPoint>?>(
		nameof(ObservationPoints),
		o => o.ObservationPoints,
		(o, v) =>
		{
			o.ObservationPoints = v;
			Dispatcher.UIThread.Invoke(o.InvalidateVisual);
		});
	private IEnumerable<ObservationPoint>? _observationPoints;
	public IEnumerable<ObservationPoint>? ObservationPoints
	{
		get => _observationPoints;
		set => SetAndRaise(ObservationPointsProperty, ref _observationPoints, value);
	}

	public static readonly DirectProperty<KmoniImageView, ObservationPoint?> SelectedObservationPointProperty =
		AvaloniaProperty.RegisterDirect<KmoniImageView, ObservationPoint?>(
			nameof(SelectedObservationPoint),
			o => o.SelectedObservationPoint,
			(o, v) => o.SelectedObservationPoint = v);
	private ObservationPoint? _selectedObservationPoint;
	public ObservationPoint? SelectedObservationPoint
	{
		get => _selectedObservationPoint;
		set {
			SetAndRaise(SelectedObservationPointProperty, ref _selectedObservationPoint, value);
			Dispatcher.UIThread.Invoke(InvalidateVisual);
		}
	}

	public static readonly DirectProperty<KmoniImageView, bool> IsShowBackgroundImageProperty =
		AvaloniaProperty.RegisterDirect<KmoniImageView, bool>(
			nameof(IsShowBackgroundImage),
			o => o.IsShowBackgroundImage,
			(o, v) =>
			{
				o.IsShowBackgroundImage = v;
				Dispatcher.UIThread.Invoke(o.InvalidateVisual);
			});
	private bool _isShowBackgroundImage = true;
	public bool IsShowBackgroundImage
	{
		get => _isShowBackgroundImage;
		set => SetAndRaise(IsShowBackgroundImageProperty, ref _isShowBackgroundImage, value);
	}

	public static readonly DirectProperty<KmoniImageView, bool> IsShowMonitorImageProperty =
		AvaloniaProperty.RegisterDirect<KmoniImageView, bool>(
			nameof(IsShowMonitorImage),
			o => o.IsShowMonitorImage,
			(o, v) =>
			{
				o.IsShowMonitorImage = v;
				Dispatcher.UIThread.Invoke(o.InvalidateVisual);
			});
	private bool _isShowMonitorImage = true;
	public bool IsShowMonitorImage
	{
		get => _isShowMonitorImage;
		set => SetAndRaise(IsShowMonitorImageProperty, ref _isShowMonitorImage, value);
	}

	public static readonly DirectProperty<KmoniImageView, bool> IsShowObservationPointProperty =
		AvaloniaProperty.RegisterDirect<KmoniImageView, bool>(
			nameof(IsShowObservationPoint),
			o => o.IsShowObservationPoint,
			(o, v) =>
			{
				o.IsShowObservationPoint = v;
				Dispatcher.UIThread.Invoke(o.InvalidateVisual);
			});
	private bool _isShowObservationPoint = true;
	public bool IsShowObservationPoint
	{
		get => _isShowObservationPoint;
		set => SetAndRaise(IsShowObservationPointProperty, ref _isShowObservationPoint, value);
	}

	public event Action<ObservationPoint>? ObservationPointUpdated;

	private HttpClient HttpClient { get; } = new();

	private SKBitmap? BackgroundImage { get; set; }
	private SKBitmap? MonitorImage { get; set; }

	private static readonly SKPaint PlaceHolderPaint = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.StrokeAndFill,
		Color = new SKColor(255, 255, 255, 50),
		PathEffect = SKPathEffect.Create2DLine(0, SKMatrix.CreateScale(8, 8).PreConcat(SKMatrix.CreateRotationDegrees(-30, 0, 0)))
	};
	private static readonly SKPaint BoderPaint = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 0, 0, 50),
	};
	private static readonly SKPaint PointPaint = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 0, 0, 50),
	};

	public KmoniImageView()
	{
		if (Design.IsDesignMode)
			return;
		Task.Run(async () =>
		{
			try
			{
				BackgroundImage = SKBitmap.Decode(await HttpClient.GetByteArrayAsync("http://www.kmoni.bosai.go.jp/data/map_img/CommonImg/base_map_w.gif"));
				MonitorImage = SKBitmap.Decode(await HttpClient.GetByteArrayAsync(WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, DateTime.UtcNow.AddHours(9).AddMinutes(-10), RealtimeDataType.Shindo)));
				Dispatcher.UIThread.Invoke(InvalidateVisual);
			}
			catch (Exception ex)
			{
				Console.WriteLine("画像のダウンロードに失敗しました" + ex);
			}
		});

		Tapped += (s, e) =>
		{
			if (e.Pointer.Type != PointerType.Mouse || !e.Pointer.IsPrimary)
				return;
			var p = ((e.GetPosition(this) - new Point(Bounds.Width / 2, Bounds.Height / 2)) / Scale) + Center;
			var ip = new Point2((int)Math.Floor(p.X), (int)Math.Floor(p.Y));
			var op = ObservationPoints?.FirstOrDefault(x => x.Point is { } po && po.X == ip.X && po.Y == ip.Y);
			if (op is not null)
				SelectedObservationPoint = op;
		};
		DoubleTapped += (s, e) =>
		{
			if (e.Pointer.Type != PointerType.Mouse || !e.Pointer.IsPrimary)
				return;
			var p = ((e.GetPosition(this) - new Point(Bounds.Width / 2, Bounds.Height / 2)) / Scale) + Center;
			var ip = new Point2((int)Math.Floor(p.X), (int)Math.Floor(p.Y));
			var op = ObservationPoints?.FirstOrDefault(x => x.Point is { } po && po.X == ip.X && po.Y == ip.Y);
			if (op is null && SelectedObservationPoint != null)
			{
				SelectedObservationPoint.Point = new Point2(ip.X, ip.Y);
				InvalidateVisual();
				ObservationPointUpdated?.Invoke(SelectedObservationPoint);
			}
		};
	}

	public Size ImageSize { get; set; } = new Size(352, 400);
	public Point Center { get; set; } = new Point(176, 200);
	public float Scale { get; set; } = 1;

	protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
	{
		base.OnPointerWheelChanged(e);

		Scale = Math.Clamp(Scale + (float)(e.Delta.Y / 5f), 1, 10);
		InvalidateVisual();
		var b = Bounds;
	}

	private Point? PrevPoint { get; set; }
	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);

		var p = e.GetPosition(this);
		if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
		{
			Center = new Point(Math.Clamp(Center.X - (p.X - PrevPoint?.X ?? 0) / Scale, 0, ImageSize.Width), Math.Clamp(Center.Y - (p.Y - PrevPoint?.Y ?? 0) / Scale, 0, ImageSize.Height));
			Cursor = new Cursor(StandardCursorType.SizeAll);
			InvalidateVisual();
		}
		PrevPoint = p;
	}
	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);
		Cursor = null;
	}

	public bool Equals(ICustomDrawOperation? other) => false;
	public override void Render(DrawingContext context)
		=> context.Custom(this);
	public bool HitTest(Point p) => true;
	public void Render(ImmediateDrawingContext context)
	{
		if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var leaseFeature))
			return;
		using var lease = leaseFeature.Lease();
		var canvas = lease.SkCanvas;

		// ちぎれるからちょっと多めに描画する
		canvas.DrawRect(-10, -10, (float)Bounds.Width + 20, (float)Bounds.Height + 20, PlaceHolderPaint);

		canvas.Save();
		try
		{
			canvas.Translate((float)((Bounds.Width / 2) - Center.X * Scale), (float)((Bounds.Height / 2) - Center.Y * Scale));
			canvas.Scale(Scale);

			if (BackgroundImage is not null && IsShowBackgroundImage)
				canvas.DrawBitmap(BackgroundImage, new SKPoint());
			canvas.DrawRect(0, 0, (float)ImageSize.Width, (float)ImageSize.Height, BoderPaint);
			if (MonitorImage is not null && IsShowMonitorImage)
				canvas.DrawBitmap(MonitorImage, new SKPoint());

			if (ObservationPoints is not null && IsShowObservationPoint)
			{
				foreach (var op in ObservationPoints)
				{
					if (op.Point is not { } p)
						continue;

					PointPaint.Color = op.Type switch
					{
						ObservationPointType.KiK_net => SKColors.Red,
						ObservationPointType.K_NET => SKColors.Orange,
						_ => SKColors.DimGray,
					};
					PointPaint.Style = op == SelectedObservationPoint ? SKPaintStyle.Fill : SKPaintStyle.Stroke;
					PointPaint.StrokeWidth = 1 / Scale;

					canvas.DrawRect(p.X, p.Y, 1, 1, PointPaint);
				}
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
	public void Dispose()
	{
		HttpClient.Dispose();
		GC.SuppressFinalize(this);
	}
}
