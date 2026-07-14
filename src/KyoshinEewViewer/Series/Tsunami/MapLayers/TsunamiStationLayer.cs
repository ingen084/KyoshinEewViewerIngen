using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Tsunami.Models;
using SkiaSharp;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Tsunami.MapLayers;
public class TsunamiStationLayer : MapLayer
{
	private TsunamiInfo? _current;
	public TsunamiInfo? Current
	{
		get => _current;
		set {
			if (_current == value) return;
			_current = value;
			// 座標キャッシュのキーとなるスナップショット配列を破棄して次回Renderで再構築させる
			_stationsSnapshot = null;
			RefreshRequest();
		}
	}

	// 描画用にフラット化した観測点のスナップショット。座標キャッシュ(配列参照単位)のキーを兼ねる
	private TsunamiObservationStation[]? _stationsSnapshot;
	// 全点分の絶対ピクセル座標のキャッシュ。配列参照+ズーム単位で再利用され、投影計算は配列差し替え時の1回で済む
	private readonly PointLayoutCache<TsunamiObservationStation> _pointLayoutCache = new(s => s.Location);

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme targetControl) { }

	private SKPaint NotObservationPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.Gray,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
	};
	private SKPaint ObservingPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.Gray.WithAlpha(220),
		Style = SKPaintStyle.Fill,
	};
	private SKPaint ObservedPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.Gold.WithAlpha(230),
		Style = SKPaintStyle.Fill,
	};
	private SKPaint LargeObservedPaint { get; } = new SKPaint
	{
		IsAntialias = true,
		Color = SKColors.MediumPurple.WithAlpha(240),
		Style = SKPaintStyle.Fill,
	};
	private SKPaint TextPaint = new()
	{
		IsAntialias = true,
		Color = SKColors.Black,
	};
	private SKFont TextFont = new(KyoshinEewViewerFonts.MainRegular, 20);

	/// <summary>
	/// 全区分の観測点を描画順(予報→注意報→警報→大津波警報→津波なし)にフラット化する
	/// </summary>
	private static TsunamiObservationStation[] BuildStationsSnapshot(TsunamiInfo info)
	{
		var stations = new List<TsunamiObservationStation>();
		void AddArea(TsunamiWarningArea[]? areas)
		{
			if (areas == null)
				return;
			foreach (var area in areas)
			{
				if (area.Stations != null)
					stations.AddRange(area.Stations);
			}
		}

		AddArea(info.ForecastAreas);
		AddArea(info.AdvisoryAreas);
		AddArea(info.WarningAreas);
		AddArea(info.MajorWarningAreas);
		AddArea(info.NoTsunamiAreas);
		return [.. stations];
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Current is not { } current)
			return;

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			var stations = _stationsSnapshot ??= BuildStationsSnapshot(current);
			// 全点分のピクセル座標のキャッシュを取得する(ズーム・配列が前回と同じ間は投影計算が発生しない)
			var pixels = _pointLayoutCache.Get(stations, param.Zoom).Pixels;

			for (var i = 0; i < stations.Length; i++)
			{
				var loc = pixels[i];
				// 座標が取得できない観測点はNaNとして保持されている
				if (double.IsNaN(loc.X))
					continue;
				if (param.PixelBound.Left > loc.X || param.PixelBound.Right < loc.X ||
					param.PixelBound.Top > loc.Y || param.PixelBound.Bottom < loc.Y)
					continue;

				var station = stations[i];
				switch (station.FirstHeightDetail)
				{
					case "ただちに来襲":
					case "津波到達中":
						canvas.DrawCircle((float)loc.X, (float)loc.Y, 5, ObservingPaint);
						break;
					case "第1波到達":
						if (station.MaxHeight is not null and >= 5)
							canvas.DrawCircle((float)loc.X, (float)loc.Y, 7, LargeObservedPaint);
						else
							canvas.DrawCircle((float)loc.X, (float)loc.Y, 6, ObservedPaint);
						break;
					default:
						canvas.DrawCircle((float)loc.X, (float)loc.Y, 3, NotObservationPaint);
						break;
				}
				if (param.Zoom >= 8)
					canvas.DrawText(station.Name, (float)loc.X + 5, (float)loc.Y + 5, SKTextAlign.Left, TextFont, TextPaint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
