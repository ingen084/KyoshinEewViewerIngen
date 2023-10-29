using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace KyoshinEewViewer.MapEditor;

public partial class MainWindow : Window
{
	private ObservableCollection<ObservationPoint> ObservationPoints { get; }
	private ObservationPointLayer ObservationPointLayer { get; } = new ObservationPointLayer();

	public MainWindow()
	{
		InitializeComponent();

		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => MapControl.RefreshResourceCache());

		ObservationPoints = new ObservableCollection<ObservationPoint>(ObservationPoint.LoadFromMpk(@"..\..\..\..\..\KyoshinEewViewer\Resources\ShindoObsPoints.mpk.lz4", true));
		DataGrid.ItemsSource = ObservationPointLayer.ObservationPoints = KmoniImageView.ObservationPoints = ObservationPoints;

		DataGrid.SelectionChanged += (s, e) => KmoniImageView.SelectedObservationPoint = DataGrid.SelectedItem as ObservationPoint;
		DataGrid.DoubleTapped += (s, e) =>
		{
			if (DataGrid.SelectedItem is not ObservationPoint p)
				return;
			MapControl.Navigate(new RectD(p.Location.Latitude, p.Location.Longitude, 0, 0), TimeSpan.FromSeconds(.3));
		};

		KmoniImageView.WhenAnyValue(x => x.SelectedObservationPoint).WhereNotNull().Subscribe(x =>
		{
			DataGrid.SelectedItem = x;
			DataGrid.ScrollIntoView(x, DataGrid.Columns.First());
		});
		KmoniImageView.ObservationPointUpdated += p => DataGrid.InvalidateArrange();
		SearchBox.KeyDown += (s, e) =>
		{
			if (e.Key == Avalonia.Input.Key.Enter)
			{
				var obs = KmoniImageView.ObservationPoints?.FirstOrDefault(x => x.Code.Contains(SearchBox.Text!));
				if (obs != null)
					KmoniImageView.SelectedObservationPoint = obs;
			}
		};

		Task.Run(async () =>
		{
			var mapData = await MapData.LoadDefaultMapAsync();
			var landLayer = new LandLayer { Map = mapData };
			var landBorderLayer = new LandBorderLayer { Map = mapData };
			MapControl.Layers = new MapLayer[] {
				landLayer,
				landBorderLayer,
				new GridLayer(),
				ObservationPointLayer,
			};
		});

		MapControl.Zoom = 6;
		MapControl.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);
	}
}

public class ObservationPointLayer : MapLayer
{
	private IEnumerable<ObservationPoint>? _observationPoints;
	public IEnumerable<ObservationPoint>? ObservationPoints
	{
		get => _observationPoints;
		set {
			_observationPoints = value;
			RefleshRequest();
		}
	}

	private ObservationPoint? _selectedObservationPoint;
	public ObservationPoint? SelectedObservationPoint
	{
		get => _selectedObservationPoint;
		set {
			_selectedObservationPoint = value;
			RefleshRequest();
		}
	}

	private static readonly SKPaint PointPaint = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
	};

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(Control targetControl) { }

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (ObservationPoints == null)
			return;
		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			foreach (var obs in ObservationPoints)
			{
				var circleSize = (param.Zoom - 4) * 1.75;
				var circleVector = new PointD(circleSize, circleSize);
				var pointCenter = obs.Location.ToPixel(param.Zoom);
				if (!param.PixelBound.IntersectsWith(new RectD(pointCenter - circleVector, pointCenter + circleVector)))
					continue;

				PointPaint.Color = obs.Type switch
				{
					ObservationPointType.KiK_net => SKColors.Red,
					ObservationPointType.K_NET => SKColors.Orange,
					_ => SKColors.DimGray,
				};
				PointPaint.Style = obs == SelectedObservationPoint ? SKPaintStyle.Fill : SKPaintStyle.Stroke;

				var tl = pointCenter - circleVector;
				canvas.DrawRect((float)tl.X, (float)tl.Y, (float)circleSize, (float)circleSize, PointPaint);
				if (param.Zoom >= 9 || obs == SelectedObservationPoint)
					canvas.DrawText(obs.Code, pointCenter.AsSkPoint(), PointPaint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
