using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Qzss.Layers;

/// <summary>
/// 指定河川洪水予報の対象河川を表示するレイヤー
/// </summary>
public class FloodLayer : MapLayer
{
	// レイヤーは複数のホストで共有されうるため、該当ズームを描画中のホストのみ更新する
	private void OnAsyncObjectGenerated(LandLayerType layerType, int zoom)
	{
		if (layerType == LandLayerType.DesignatedRiver)
			RefreshRequest(param => (int)Math.Ceiling(param.Zoom) == zoom);
	}

	private MapData? _map;
	public MapData? Map
	{
		get => _map;
		set
		{
			if (_map != null)
				_map.AsyncObjectGenerated -= OnAsyncObjectGenerated;
			_map = value;
			if (_map != null)
				_map.AsyncObjectGenerated += OnAsyncObjectGenerated;
			RefreshRequest();
		}
	}

	private Dictionary<long, byte> _rivers = [];
	/// <summary>
	/// 表示する河川 (洪水予報区のコード → 電文上の警戒レベル)
	/// </summary>
	public IReadOnlyDictionary<long, byte> Rivers
	{
		get => _rivers;
		set
		{
			_rivers = new(value);
			RefreshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	// SKPaintは全レイヤーインスタンスで共有する(コンポジタスレッドのRenderとRefreshResourceCacheの競合、
	// および電文グループごとのレイヤー生成によるリークを避けるため、Dispose・再生成はせず色プロパティの差し替えのみ行う)
	private static readonly SKPaint BorderPaint = CreateLinePaint();
	private static readonly SKPaint CancelPaint = CreateLinePaint();
	private static readonly SKPaint AdvisoryPaint = CreateLinePaint();
	private static readonly SKPaint WarningPaint = CreateLinePaint();
	private static readonly SKPaint MajorWarningPaint = CreateLinePaint();

	private static SKPaint CreateLinePaint() => new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeCap = SKStrokeCap.Round,
		StrokeJoin = SKStrokeJoin.Round,
		IsAntialias = true,
	};

	/// <summary>
	/// 河川が重なった場合に深刻なほうが隠れないよう、軽いレベルから順に描画する
	/// </summary>
	private static readonly SKPaint[] RenderOrder = [CancelPaint, AdvisoryPaint, WarningPaint, MajorWarningPaint];

	// 色分けは一覧表示の FloodWarningColor に合わせる
	// (警報解除のみ、地図では地形と輝度差が付かないため前景色側を使う)
	private static SKPaint GetPaint(byte warningType)
		=> warningType switch
		{
			2 => AdvisoryPaint,
			3 => WarningPaint,
			4 => MajorWarningPaint,
			_ => CancelPaint,
		};

	/// <summary>
	/// ズームに応じた線の太さ(画面ピクセル)
	/// </summary>
	private static float GetLineWidth(double zoom)
		=> (float)Math.Max(2, 3 + (zoom - 5) * .8);

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		BorderPaint.Color = windowTheme.IsDark ? SKColors.Black : SKColors.White;
		// 一覧では警報解除に DockTitleBackgroundColor を使っているが、パネルの背景色のため
		// 地図に塗ると地形とほとんど区別が付かない。他のレベルは色で判別できるのに対し
		// 警報解除は無彩色なので、前景色側を使う
		CancelPaint.Color = SKColor.Parse(windowTheme.SubForegroundColor);
		AdvisoryPaint.Color = SKColor.Parse(windowTheme.TsunamiAdvisoryColor);
		WarningPaint.Color = SKColor.Parse(windowTheme.TsunamiWarningColor);
		MajorWarningPaint.Color = SKColor.Parse(windowTheme.TsunamiMajorWarningColor);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Map == null || Rivers.Count <= 0)
			return;

		lock (Map)
		{
			if (!Map.TryGetLayer(LandLayerType.DesignatedRiver, out var layer))
				return;

			canvas.Save();
			try
			{
				// 使用するキャッシュのズーム
				var baseZoom = (int)Math.Ceiling(param.Zoom);
				// 実際のズームに合わせるためのスケール
				var scale = Math.Pow(2, param.Zoom - baseZoom);
				canvas.Scale((float)scale);
				// 画面座標への変換
				var leftTop = param.LeftTopLocation.CastLocation().ToPixel(baseZoom);
				canvas.Translate((float)-leftTop.X, (float)-leftTop.Y);

				// 線の太さはズームだけから決まるため、同時に描画される他のレイヤーとも同じ値になる
				var width = (float)(GetLineWidth(param.Zoom) / scale);
				BorderPaint.StrokeWidth = (float)(width + 3 / scale);
				foreach (var paint in RenderOrder)
					paint.StrokeWidth = width;

				// 縁取りを先にまとめて描き、その上に軽いレベルから順に色を重ねる
				// (河川ごとに縁取りと色を交互に描くと、交差部で後から描いた縁取りが先の色を隠すため)
				foreach (var f in layer.PolyFeatures)
					if (TryGetPaint(f, param, out _))
						f.DrawAsPolyline(canvas, baseZoom, BorderPaint);
				foreach (var levelPaint in RenderOrder)
					foreach (var f in layer.PolyFeatures)
						if (TryGetPaint(f, param, out var paint) && paint == levelPaint)
							f.DrawAsPolyline(canvas, baseZoom, paint);
			}
			finally
			{
				canvas.Restore();
			}
		}
	}

	/// <summary>
	/// 表示対象かつ画面内の河川であれば描画に使うブラシを返す
	/// </summary>
	private bool TryGetPaint(PolygonFeature feature, LayerRenderParameter param, out SKPaint paint)
	{
		paint = null!;
		if (feature.Code is not { } code || !_rivers.TryGetValue(code, out var warningType))
			return false;
		if (!param.ViewAreaRect.IntersectsWith(feature.BoundingBox))
			return false;
		paint = GetPaint(warningType);
		return true;
	}
}
