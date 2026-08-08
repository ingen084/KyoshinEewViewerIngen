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
/// 海上警報の背景レイヤー（地形より下に描画）
/// </summary>
public class MarineWarningLayer : MapLayer
{
	private void OnAsyncObjectGenerated(LandLayerType layerType, int _)
	{
		if (layerType == LandLayerType.LocalMarineForecastArea)
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

	private MarineReportGroup? _current;
	public MarineReportGroup? Current
	{
		get => _current;
		set {
			if (_current == value) return;
			_current = value;
			RefreshRequest();
		}
	}

	public void RequestRefresh() => RefreshRequest();

	public override bool NeedPersistentUpdate => false;

	// 風系警報の塗りつぶし用Paint
	private SKPaint _typhoonFillPaint = new();      // 海上台風警報
	private SKPaint _stormFillPaint = new();        // 海上暴風警報
	private SKPaint _galeFillPaint = new();         // 海上強風警報
	private SKPaint _windFillPaint = new();         // 海上風警報

	// ボーダー用Paint
	private SKPaint _borderPaint = new();
	private float _borderStrokeWidth = 0.6f;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		_typhoonFillPaint.Dispose();
		_typhoonFillPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = SKColor.Parse(windowTheme.MarineWarningTyphoonColor),
			IsAntialias = false,
		};

		_stormFillPaint.Dispose();
		_stormFillPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = SKColor.Parse(windowTheme.MarineWarningStormColor),
			IsAntialias = false,
		};

		_galeFillPaint.Dispose();
		_galeFillPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = SKColor.Parse(windowTheme.MarineWarningGaleColor),
			IsAntialias = false,
		};

		_windFillPaint.Dispose();
		_windFillPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = SKColor.Parse(windowTheme.MarineWarningWindColor),
			IsAntialias = false,
		};

		_borderPaint.Dispose();
		_borderPaint = new SKPaint
		{
			Style = SKPaintStyle.Stroke,
			Color = SKColor.Parse(windowTheme.PrefStrokeColor),
			IsAntialias = true,
			StrokeCap = SKStrokeCap.Square,
			StrokeJoin = SKStrokeJoin.Round,
		};
		_borderStrokeWidth = (float)windowTheme.PrefStrokeThickness;
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Map == null)
			return;

		lock (Map)
		{
			if (Current == null || !Map.TryGetLayer(LandLayerType.LocalMarineForecastArea, out var layer))
				return;

			canvas.Save();
			try
			{
				var baseZoom = (int)Math.Ceiling(param.Zoom);
				var scale = Math.Pow(2, param.Zoom - baseZoom);
				canvas.Scale((float)scale);
				var leftTop = param.LeftTopLocation.CastLocation().ToPixel(baseZoom);
				canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

				_borderPaint.StrokeWidth = (float)(_borderStrokeWidth / scale);

				// NOTE: 毎フレーム計算するのは微妙だけど軽いので一旦おいとく
				// 警報地域のコードと最優先警報コードのマップを事前に構築
				// アイコン表示の対象（濃霧11、着氷10、うねり12）は塗りつぶし対象から除外
				Dictionary<int, byte>? warningMap = null;
				if (Current.AggregatedRegions.Count > 0)
				{
					warningMap = [];
					foreach (var area in Current.AggregatedRegions)
					{
						var activeWarnings = area.WarningCodes.Where(w => w != 0 && w != 10 && w != 11 && w != 12).ToList();
						if (activeWarnings.Count == 0)
							continue;
						var highestPriority = activeWarnings.MinBy(GetWarningPriority);
						warningMap[area.AreaCode] = highestPriority;
					}
				}

				// 表示領域内のポリゴンのみを処理
				foreach (var f in layer.FindPolygon(param.ViewAreaRect))
				{
					// 警報が発表されている地域の場合は塗りつぶし
					if (warningMap != null && f.Code is { } code &&
					 	TryGetWarningCode(warningMap, code, out var warningCode) &&
						GetFillPaintForWarningCode(warningCode) is { } fillPaint)
						f.Draw(canvas, baseZoom, fillPaint);
				}

				// ボーダーをPolyLineで描画
				for (var i = 0; i < layer.LineFeatures.Length; i++)
				{
					var f = layer.LineFeatures[i];
					if (!param.ViewAreaRect.IntersectsWith(f.BoundingBox))
						continue;
					f.Draw(canvas, baseZoom, _borderPaint);
				}
			}
			finally
			{
				canvas.Restore();
			}
		}
	}

	private SKPaint? GetFillPaintForWarningCode(byte warningCode)
		=> warningCode switch
		{
			23 => _typhoonFillPaint,
			22 => _stormFillPaint,
			21 => _galeFillPaint,
			20 => _windFillPaint,
			_ => null,
		};

	internal static int GetWarningPriority(byte warningCode)
		=> warningCode switch
		{
			23 => 0, // 海上台風警報
			22 => 1, // 海上暴風警報
			21 => 2, // 海上強風警報
			20 => 3, // 海上風警報
			12 => 4, // 海上うねり警報
			11 => 5, // 海上濃霧警報
			10 => 6, // 海上着氷警報
			_ => 99,
		};

	/// <summary>
	/// 警報コードを取得する（親地域コードにも対応）
	/// </summary>
	/// <param name="warningMap">警報マップ</param>
	/// <param name="code">地域コード</param>
	/// <param name="warningCode">警報コード</param>
	/// <returns>警報が存在する場合はtrue</returns>
	internal static bool TryGetWarningCode(Dictionary<int, byte> warningMap, int code, out byte warningCode)
	{
		// 直接一致
		if (warningMap.TryGetValue(code, out warningCode))
			return true;

		// 親地域（下2桁が00）による一致をチェック
		var parentCode = code / 100 * 100;
		if (parentCode != code && warningMap.TryGetValue(parentCode, out warningCode))
			return true;

		warningCode = 0;
		return false;
	}
}
