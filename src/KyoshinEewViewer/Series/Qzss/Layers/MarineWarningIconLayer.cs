using System;
using System.Collections.Generic;
using System.Linq;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Qzss.Models;
using SkiaSharp;

namespace KyoshinEewViewer.Series.Qzss.Layers;

/// <summary>
/// 海上警報のアイコンレイヤー
/// </summary>
public class MarineWarningIconLayer : MapLayer
{
	public MapData? Map { get; set; }

	private MarineReportGroup? _current;
	public MarineReportGroup? Current
	{
		get => _current;
		set
		{
			if (_current == value) return;
			_current = value;
			RefreshRequest();
		}
	}

	public void RequestRefresh() => RefreshRequest();

	public override bool NeedPersistentUpdate => false;

	// 濃霧アイコン用Paint
	private SKPaint _fogIconFillPaint = new();
	private SKPaint _fogIconStrokePaint = new();

	// 着氷アイコン用Paint
	private SKPaint _icingIconFillPaint = new();
	private SKPaint _icingIconStrokePaint = new();

	// うねりアイコン用Paint
	private SKPaint _swellIconFillPaint = new();
	private SKPaint _swellIconStrokePaint = new();

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		_fogIconFillPaint.Dispose();
		_fogIconFillPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = new SKColor(0x60, 0x60, 0x60, 0xFF),
			IsAntialias = true,
		};

		_fogIconStrokePaint.Dispose();
		_fogIconStrokePaint = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = new SKColor(0xFF, 0xFF, 0xFF, 0xFF),
			IsAntialias = true,
		};

		_icingIconFillPaint.Dispose();
		_icingIconFillPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = new SKColor(0x40, 0x80, 0xC0, 0xFF),
			IsAntialias = true,
		};

		_icingIconStrokePaint.Dispose();
		_icingIconStrokePaint = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = new SKColor(0xFF, 0xFF, 0xFF, 0xFF),
			IsAntialias = true,
		};

		_swellIconFillPaint.Dispose();
		_swellIconFillPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = new SKColor(0x00, 0x80, 0xE0, 0xFF),
			IsAntialias = true,
		};

		_swellIconStrokePaint.Dispose();
		_swellIconStrokePaint = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = new SKColor(0xFF, 0xFF, 0xFF, 0xFF),
			IsAntialias = true,
		};
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Current == null || Current.AggregatedRegions.Count == 0)
			return;

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			// NOTE: 毎フレーム計算するのは微妙だけど軽いので一旦おいとく
			// 警報地域のコードと濃霧・着氷・うねりの有無を構築
			var iconMap = new Dictionary<int, (bool HasFog, bool HasIcing, bool HasSwell)>();
			foreach (var area in Current.AggregatedRegions)
			{
				var activeWarnings = area.WarningCodes.Where(w => w != 0).ToList();
				if (activeWarnings.Count == 0)
					continue;

				var hasFog = activeWarnings.Contains<byte>(11);
				var hasIcing = activeWarnings.Contains<byte>(10);
				var hasSwell = activeWarnings.Contains<byte>(12);
				if (hasFog || hasIcing || hasSwell)
					iconMap[area.AreaCode] = (hasFog, hasIcing, hasSwell);
			}

			// 濃霧・着氷警報の地域にアイコンを描画（ズームレベルに応じてサイズ変更）
			var iconSize = (float)(Math.Max(1, param.Zoom - 3) * 5);
			var iconRadius = iconSize / 2 + 2;
			_fogIconStrokePaint.StrokeWidth = 2;
			_icingIconStrokePaint.StrokeWidth = 2;

			// FeatureLayerを取得
			FeatureLayer? layer = null;
			Map?.TryGetLayer(LandLayerType.LocalMarineForecastArea, out layer);

			foreach (var (code, (hasFog, hasIcing, hasSwell)) in iconMap)
			{
				// 中心座標を取得する地域コードのリストを作成
				var targetCodes = GetTargetCodes(code, layer);

				foreach (var targetCode in targetCodes)
				{
					var centerLocation = RegionCenterLocations.Default.GetLocation(LandLayerType.LocalMarineForecastArea, targetCode);
					if (centerLocation == null)
						continue;

					var centerPixel = centerLocation.ToPixel(param.Zoom);

					// 表示するアイコンの数をカウント
					var iconCount = (hasFog ? 1 : 0) + (hasIcing ? 1 : 0) + (hasSwell ? 1 : 0);
					var spacing = iconRadius * 2 + 4;
					var startX = (float)centerPixel.X - (iconCount - 1) * spacing / 2;
					var currentX = startX;

					if (hasFog)
					{
						DrawFogIcon(canvas, currentX, (float)centerPixel.Y, iconSize);
						currentX += spacing;
					}
					if (hasIcing)
					{
						DrawIcingIcon(canvas, currentX, (float)centerPixel.Y, iconSize);
						currentX += spacing;
					}
					if (hasSwell)
					{
						DrawSwellIcon(canvas, currentX, (float)centerPixel.Y, iconSize);
					}
				}
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	private void DrawFogIcon(SKCanvas canvas, float x, float y, float size)
	{
		// 霧アイコン: 横線3本
		var halfSize = size / 2;
		var lineSpacing = size / 3;

		// 背景円
		canvas.DrawCircle(x, y, halfSize + 2, _fogIconStrokePaint);
		canvas.DrawCircle(x, y, halfSize + 2, _fogIconFillPaint);

		// 横線を描画
		_fogIconStrokePaint.StrokeWidth = size / 6;
		var lineHalfWidth = halfSize * 0.6f;
		canvas.DrawLine(x - lineHalfWidth, y - lineSpacing * 0.8f, x + lineHalfWidth, y - lineSpacing * 0.8f, _fogIconStrokePaint);
		canvas.DrawLine(x - lineHalfWidth, y, x + lineHalfWidth, y, _fogIconStrokePaint);
		canvas.DrawLine(x - lineHalfWidth, y + lineSpacing * 0.8f, x + lineHalfWidth, y + lineSpacing * 0.8f, _fogIconStrokePaint);
	}

	private void DrawIcingIcon(SKCanvas canvas, float x, float y, float size)
	{
		// 着氷アイコン: 雪の結晶風
		var halfSize = size / 2;

		// 背景円
		canvas.DrawCircle(x, y, halfSize + 2, _icingIconStrokePaint);
		canvas.DrawCircle(x, y, halfSize + 2, _icingIconFillPaint);

		// 結晶を描画（6方向の線）
		_icingIconStrokePaint.StrokeWidth = size / 8;
		var lineLength = halfSize * 0.7f;
		for (var i = 0; i < 6; i++)
		{
			var angle = i * Math.PI / 3;
			var dx = (float)(Math.Cos(angle) * lineLength);
			var dy = (float)(Math.Sin(angle) * lineLength);
			canvas.DrawLine(x, y, x + dx, y + dy, _icingIconStrokePaint);
		}
	}

	private void DrawSwellIcon(SKCanvas canvas, float x, float y, float size)
	{
		// うねりアイコン: 波形
		var halfSize = size / 2;

		// 背景円
		canvas.DrawCircle(x, y, halfSize + 2, _swellIconStrokePaint);
		canvas.DrawCircle(x, y, halfSize + 2, _swellIconFillPaint);

		// 波形を描画（2本の波線）
		_swellIconStrokePaint.StrokeWidth = size / 6;
		var waveWidth = halfSize * 0.7f;
		var waveHeight = halfSize * 0.3f;

		// 上の波
		using var path1 = new SKPath();
		path1.MoveTo(x - waveWidth, y - waveHeight);
		path1.QuadTo(x - waveWidth / 2, y - waveHeight * 2, x, y - waveHeight);
		path1.QuadTo(x + waveWidth / 2, y, x + waveWidth, y - waveHeight);
		canvas.DrawPath(path1, _swellIconStrokePaint);

		// 下の波
		using var path2 = new SKPath();
		path2.MoveTo(x - waveWidth, y + waveHeight);
		path2.QuadTo(x - waveWidth / 2, y, x, y + waveHeight);
		path2.QuadTo(x + waveWidth / 2, y + waveHeight * 2, x + waveWidth, y + waveHeight);
		canvas.DrawPath(path2, _swellIconStrokePaint);
	}

	/// <summary>
	/// 対象の地域コードを取得する
	/// 親地域コード（下2桁が00）の場合はFeatureLayerから子地域コードを検索
	/// </summary>
	private static IEnumerable<int> GetTargetCodes(int code, FeatureLayer? layer)
	{
		// 下2桁が00でない場合はそのまま返す
		if (code % 100 != 0)
		{
			yield return code;
			yield break;
		}

		// 親地域コードの場合はFeatureLayerから子地域を検索
		if (layer == null)
			yield break;

		var parentCode = code / 100;
		foreach (var poly in layer.FindPolygon(parentCode, 100))
		{
			if (poly.Code is { } childCode)
				yield return childCode;
		}
	}
}
