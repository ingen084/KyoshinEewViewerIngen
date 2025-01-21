using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public record TsunamiArea(int Code, string Name, string Status, string Height);
public class TsunamiReportGroup : DCReportGroup
{
	private MapData? MapData { get; }
	private TsunamiBorderLayer Layer { get; }

	public List<TsunamiReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private byte _warningCode;
	public byte WarningCode
	{
		get => _warningCode;
		set => this.RaiseAndSetIfChanged(ref _warningCode, value);
	}

	public ObservableCollection<TsunamiArea> NoTsunamiAreas { get; } = [];
	public ObservableCollection<TsunamiArea> ClearedAreas { get; } = [];
	public ObservableCollection<TsunamiArea> WarningAreas { get; } = [];
	public ObservableCollection<TsunamiArea> MajorWarningAreas { get; } = [];
	public ObservableCollection<TsunamiArea> OtherWarningAreas { get; } = [];

	public TsunamiReportGroup(TsunamiReport report, MapData? mapData)
	{
		MapData = mapData;
		Layer = new()
		{
			Current = this,
			Map = mapData
		};

		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		WarningCode = report.WarningCode;

		Reports.Add(report);

		UpdateDetails();
	}

	public override bool CheckDuplicate(DCReport report) => report is TsunamiReport t && Reports.Any(r => t.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not TsunamiReport t || t.ReportTime.LocalDateTime != ReportTime)
			return false;

		Reports.Add(t);
		if (t.WarningCode > WarningCode)
			WarningCode = t.WarningCode;
		ReportCount++;
		TotalAreaCount += t.Regions.Count(a => a.Region != 0);

		UpdateDetails();
		return true;
	}

	private static string GetTsunamiHeightString(byte height)
		=> height switch
		{
			1 => "0.2m未満",
			2 => "1m",
			3 => "3m",
			4 => "5m",
			5 => "10m",
			6 => "10m超",
			14 => "不明",
			15 => "その他",
			_ => $"不明({height})",
		};
	private static string GetTsunamiAreaName(int region)
		=> region switch
		{
			990 => "予約済み",
			1000 => "その他",
			_ => CsvDictionary.AreaTsunami.TryGetValue(region, out var name) ? name : $"不明({region})"
		};
	private void UpdateDetails()
	{
		NoTsunamiAreas.Clear();
		ClearedAreas.Clear();
		WarningAreas.Clear();
		MajorWarningAreas.Clear();
		OtherWarningAreas.Clear();

		var zoomPoints = new List<KyoshinMonitorLib.Location>();
		FeatureLayer? tsunamiLayer = null;
		MapData?.TryGetLayer(LandLayerType.TsunamiForecastArea, out tsunamiLayer);

		foreach (var report in Reports)
		{
			var area = report.WarningCode switch
			{
				1 => NoTsunamiAreas,
				2 => ClearedAreas,
				3 => WarningAreas,
				4 or 5 => MajorWarningAreas,
				_ => OtherWarningAreas,
			};

			foreach (var r in report.Regions)
			{
				if (r.Region == 0)
					continue;
				area.Add(new(r.Region, GetTsunamiAreaName(r.Region), r.IsArrived ? "到達" : r.ArrivalTime.ToString("HH:mm 到達見込み"), GetTsunamiHeightString(r.Height)));

				if (tsunamiLayer != null)
				{
					foreach (var p in tsunamiLayer.FindPolygon(r.Region))
					{
						zoomPoints.Add(p.BoundingBox.TopLeft.CastLocation());
						zoomPoints.Add(p.BoundingBox.BottomRight.CastLocation());
					}
				}
			}
		}

		MapDisplayParameter = new()
		{
			BackgroundLayers = [Layer],
			Padding = new(355, 0, 0, 0),
		};

		if (zoomPoints.Count <= 0)
		{
			MapNavigationRequest = null;
			return;
		}

		// 自動ズーム範囲を計算
		var minLat = float.MaxValue;
		var maxLat = float.MinValue;
		var minLng = float.MaxValue;
		var maxLng = float.MinValue;
		foreach (var p in zoomPoints)
		{
			if (minLat > p.Latitude)
				minLat = p.Latitude;
			if (minLng > p.Longitude)
				minLng = p.Longitude;

			if (maxLat < p.Latitude)
				maxLat = p.Latitude;
			if (maxLng < p.Longitude)
				maxLng = p.Longitude;
		}
		MapNavigationRequest = new(new Avalonia.Rect(minLat - 1, minLng - 1, maxLat - minLat + 2, maxLng - minLng + 3));
	}

	public override Control? DetailDisplayControl => new TsunamiReportControl { DataContext = this };
}

public class TsunamiBorderLayer : MapLayer
{
	private int LastZoomLevel { get; set; }
	private void OnAsyncObjectGenerated(LandLayerType layerType, int zoom)
	{
		if (LastZoomLevel == zoom && layerType == LandLayerType.TsunamiForecastArea)
			RefreshRequest();
	}


	private MapData? _map;
	public MapData? Map
	{
		get => _map;
		set {
			if (_map != null)
				_map.AsyncObjectGenerated -= OnAsyncObjectGenerated;
			_map = value;
			if (_map != null)
				_map.AsyncObjectGenerated += OnAsyncObjectGenerated;
			RefreshRequest();
		}
	}

	private TsunamiReportGroup? _current;
	public TsunamiReportGroup? Current
	{
		get => _current;
		set {
			if (_current == value) return;
			_current = value;
			RefreshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	private SKPaint _majorWarningPaint = new();
	private SKPaint _warningPaint = new();

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		_majorWarningPaint.Dispose();
		_majorWarningPaint = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColor.Parse(windowTheme.TsunamiMajorWarningColor),
			IsAntialias = true,
			StrokeCap = SKStrokeCap.Square,
			StrokeJoin = SKStrokeJoin.Round,
		};

		_warningPaint.Dispose();
		_warningPaint = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColor.Parse(windowTheme.TsunamiWarningColor),
			IsAntialias = true,
			StrokeCap = SKStrokeCap.Square,
			StrokeJoin = SKStrokeJoin.Round,
		};
	}
	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Map == null)
			return;

		lock (Map)
		{
			if (Current == null || !Map.TryGetLayer(LandLayerType.TsunamiForecastArea, out var layer))
				return;

			canvas.Save();
			try
			{
				// 使用するキャッシュのズーム
				var baseZoom = (int)Math.Ceiling(param.Zoom);
				LastZoomLevel = baseZoom;
				// 実際のズームに合わせるためのスケール
				var scale = Math.Pow(2, param.Zoom - baseZoom);
				canvas.Scale((float)scale);
				// 画面座標への変換
				var leftTop = param.LeftTopLocation.CastLocation().ToPixel(baseZoom);
				canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

				// スケールに合わせてブラシのサイズ変更
				_majorWarningPaint.StrokeWidth = (float)(12 / scale);
				_warningPaint.StrokeWidth = (float)(8 / scale);

				if (Current.WarningAreas != null)
				{
					for (var i = 0; i < layer.PolyFeatures.Length; i++)
					{
						var f = layer.PolyFeatures[i];
						if (Current.WarningAreas.Any(a => a.Code == f.Code) && param.ViewAreaRect.IntersectsWith(f.BoundingBox))
							f.DrawAsPolyline(canvas, baseZoom, _warningPaint);
					}
				}
				if (Current.MajorWarningAreas != null)
				{
					for (var i = 0; i < layer.PolyFeatures.Length; i++)
					{
						var f = layer.PolyFeatures[i];
						if (Current.MajorWarningAreas.Any(a => a.Code == f.Code) && param.ViewAreaRect.IntersectsWith(f.BoundingBox))
							f.DrawAsPolyline(canvas, baseZoom, _majorWarningPaint);
					}
				}
			}
			finally
			{
				canvas.Restore();
			}
		}
	}
}
