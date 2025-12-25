using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

/// <summary>
/// 揺れ検知範囲を描画するレイヤー
/// </summary>
public class ShakeDetectionAreaLayer(KyoshinEewViewerConfiguration config, KyoshinMonitorSeries hostSeries) : MapLayer
{
	private KyoshinEewViewerConfiguration Config { get; } = config;
	private KyoshinMonitorSeries HostSeries { get; } = hostSeries;

	/// <summary>
	/// オフセット距離（km）
	/// </summary>
	public double OffsetDistanceKm { get; set; } = 20.0;

	/// <summary>
	/// グリッドセルのサイズ（ピクセル）
	/// </summary>
	private const float GridCellSize = 150f;

	/// <summary>
	/// パルスアニメーション周期（ミリ秒）
	/// </summary>
	private const double PulseAnimationPeriodMs = 2000.0;

	/// <summary>
	/// 点滅アニメーション周期（ミリ秒）
	/// </summary>
	private const double BlinkAnimationPeriodMs = 1000.0;

	/// <summary>
	/// パス生成の基準ズームレベル
	/// </summary>
	private const double BaseZoom = 10.0;

	private KyoshinEvent[]? _kyoshinEvents;
	/// <summary>
	/// 描画対象のKyoshinEvent配列
	/// </summary>
	public KyoshinEvent[]? KyoshinEvents
	{
		get => _kyoshinEvents;
		set {
			_kyoshinEvents = value;
			InvalidateCache();
			RefreshRequest();
		}
	}

	// レベル別の基準色（グリッド・凸包共通）
	private static readonly SKColor[] LevelBaseColors =
	[
		new SKColor(128, 128, 128), // Weaker - グレー
		new SKColor(100, 150, 255), // Weak - 青
		new SKColor(255, 255, 0),   // Medium - 黄色
		new SKColor(255, 165, 0),   // Strong - オレンジ
		new SKColor(255, 0, 0),     // Stronger - 赤
	];

	// レベル別の枠線の太さ（凸包用）
	private static readonly float[] LevelStrokeWidths = [1f, 1f, 1.5f, 2f, 2f];

	private record CachedPath(Guid EventId, int PointCount, SKPath Path);
	private Dictionary<Guid, CachedPath> PathCache { get; } = [];

	/// <summary>
	/// グリッドの原点座標（地理座標）
	/// </summary>
	private Location? GridOriginLocation { get; set; }

	/// <summary>
	/// アニメーション用ストップウォッチ（明滅用）
	/// </summary>
	private readonly Stopwatch _animationStopwatch = Stopwatch.StartNew();

	/// <summary>
	/// 点滅アニメーション用タイマー
	/// </summary>
	private Timer? _blinkTimer;

	/// <summary>
	/// 点滅の現在の状態（true: 明るい、false: 暗い）
	/// </summary>
	private bool _blinkState = true;

	/// <summary>
	/// 明滅アニメーションがアクティブかどうか（毎フレーム更新が必要）
	/// </summary>
	private bool _isPulseAnimationActive;

	public override bool NeedPersistentUpdate => _isPulseAnimationActive;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		// 将来的にテーマから色を取得する場合はここで設定
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		// EEW発表中は描画しない
		if (HostSeries.CurrentInformationHost.Eews?.Length > 0)
		{
			StopAnimations();
			return;
		}

		var displayMode = Config.KyoshinMonitor.ShakeDetectionDisplayMode;
		var animationMode = Config.KyoshinMonitor.ShakeDetectionAnimationMode;

		// 表示しないモードの場合
		if (displayMode == ShakeDetectionDisplayMode.None)
		{
			StopAnimations();
			return;
		}

		// KyoshinEventsのスナップショットを取得（スレッドセーフ）
		var events = KyoshinEvents;
		if (events == null || events.Length == 0)
		{
			StopAnimations();
			GridOriginLocation = null;
			return;
		}

		// 確定した観測点を収集（スレッドセーフ）
		var confirmedPoints = CollectConfirmedPointsSafe(events);

		if (confirmedPoints.Count == 0)
		{
			StopAnimations();
			GridOriginLocation = null;
			return;
		}

		// アニメーションモードに応じて制御
		UpdateAnimationState(animationMode);

		switch (displayMode)
		{
			case ShakeDetectionDisplayMode.ConvexHull:
				RenderConvexHull(canvas, param, animationMode);
				break;
			case ShakeDetectionDisplayMode.Grid:
				RenderGrid(canvas, param, confirmedPoints, animationMode);
				break;
		}
	}

	/// <summary>
	/// アニメーション状態を更新する
	/// </summary>
	private void UpdateAnimationState(ShakeDetectionAnimationMode animationMode)
	{
		switch (animationMode)
		{
			case ShakeDetectionAnimationMode.None:
				StopAnimations();
				break;
			case ShakeDetectionAnimationMode.Blink:
				_isPulseAnimationActive = false;
				StartBlinkTimer();
				break;
			case ShakeDetectionAnimationMode.Pulse:
				StopBlinkTimer();
				_isPulseAnimationActive = true;
				break;
		}
	}

	/// <summary>
	/// 全てのアニメーションを停止する
	/// </summary>
	private void StopAnimations()
	{
		_isPulseAnimationActive = false;
		StopBlinkTimer();
	}

	/// <summary>
	/// 点滅タイマーを開始する
	/// </summary>
	private void StartBlinkTimer()
	{
		if (_blinkTimer != null)
			return;

		_blinkTimer = new Timer(BlinkAnimationPeriodMs / 2)
		{
			AutoReset = true
		};
		_blinkTimer.Elapsed += OnBlinkTimerElapsed;
		_blinkTimer.Start();
	}

	/// <summary>
	/// 点滅タイマーを停止する
	/// </summary>
	private void StopBlinkTimer()
	{
		if (_blinkTimer == null)
			return;

		_blinkTimer.Stop();
		_blinkTimer.Elapsed -= OnBlinkTimerElapsed;
		_blinkTimer.Dispose();
		_blinkTimer = null;
		_blinkState = true;
	}

	/// <summary>
	/// 点滅タイマーのElapsedイベントハンドラ
	/// </summary>
	private void OnBlinkTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		_blinkState = !_blinkState;
		RefreshRequest();
	}

	/// <summary>
	/// スレッドセーフに確定した観測点を収集する
	/// </summary>
	private static List<RealtimeObservationPoint> CollectConfirmedPointsSafe(KyoshinEvent[] events)
	{
		var result = new List<RealtimeObservationPoint>();

		foreach (var evt in events)
		{
			if (!evt.IsConfirmed || evt.PointCount == 0)
				continue;

			try
			{
				// Pointsのスナップショットを取得してからイテレーション
				var points = evt.Points.ToArray();
				foreach (var point in points)
				{
					if (point != null)
						result.Add(point);
				}
			}
			catch (InvalidOperationException)
			{
				// コレクションが変更された場合はこのイベントをスキップ
			}
		}

		return result;
	}

	private void RenderConvexHull(SKCanvas canvas, LayerRenderParameter param, ShakeDetectionAnimationMode animationMode)
	{
		if (KyoshinEvents == null)
			return;

		// アニメーション係数を計算
		var animationFactor = CalculateAnimationFactor(animationMode);

		// 基準ズームからのスケール係数を計算
		var scale = (float)Math.Pow(2, param.Zoom - BaseZoom);

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);
			canvas.Scale(scale);

			foreach (var evt in KyoshinEvents)
			{
				if (!evt.IsConfirmed)
					continue;

				if (evt.PointCount == 0)
					continue;

				var path = GetOrCreatePath(evt);
				if (path == null)
					continue;

				var levelIndex = (int)evt.Level;
				if (levelIndex < 0 || levelIndex >= LevelBaseColors.Length)
					continue;

				// アニメーションを適用した枠線のみ描画（塗りつぶしなし）
				var baseColor = LevelBaseColors[levelIndex];
				var baseStrokeWidth = LevelStrokeWidths[levelIndex];
				var animatedAlpha = (byte)(80 + 140 * animationFactor);
				var animatedStrokeWidth = baseStrokeWidth * (0.5f + 0.5f * animationFactor);

				using var strokePaint = new SKPaint
				{
					Style = SKPaintStyle.Stroke,
					IsAntialias = true,
					StrokeWidth = animatedStrokeWidth,
					Color = baseColor.WithAlpha(animatedAlpha)
				};
				canvas.DrawPath(path, strokePaint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	private void RenderGrid(SKCanvas canvas, LayerRenderParameter param, List<RealtimeObservationPoint> confirmedPoints, ShakeDetectionAnimationMode animationMode)
	{
		// グリッド原点が未設定の場合、最初の観測点を原点として設定
		GridOriginLocation ??= confirmedPoints[0].Location;

		// アニメーション係数を計算
		var animationFactor = CalculateAnimationFactor(animationMode);

		// グリッド原点のスクリーン座標を計算
		var originPixel = GridOriginLocation.ToPixel(param.Zoom);
		var originScreenX = (float)(originPixel.X - param.LeftTopPixel.X);
		var originScreenY = (float)(originPixel.Y - param.LeftTopPixel.Y);

		// 各観測点をグリッドセルに割り当て
		var gridCells = new Dictionary<(int, int), (KyoshinEventLevel level, double maxIntensity)>();

		foreach (var point in confirmedPoints)
		{
			var pointPixel = point.Location.ToPixel(param.Zoom);
			var screenX = (float)(pointPixel.X - param.LeftTopPixel.X);
			var screenY = (float)(pointPixel.Y - param.LeftTopPixel.Y);

			// グリッド原点からの相対位置
			var relX = screenX - originScreenX;
			var relY = screenY - originScreenY;

			// グリッドセルのインデックスを計算（原点が中央になるようにオフセット）
			var cellX = (int)Math.Floor((relX + GridCellSize / 2) / GridCellSize);
			var cellY = (int)Math.Floor((relY + GridCellSize / 2) / GridCellSize);

			var cellKey = (cellX, cellY);
			var intensity = point.LatestIntensity ?? -3;
			var level = KyoshinEvent.GetLevel(intensity);

			if (gridCells.TryGetValue(cellKey, out var existing))
			{
				// 最大震度を更新
				if (intensity > existing.maxIntensity)
					gridCells[cellKey] = (level, intensity);
			}
			else
			{
				gridCells[cellKey] = (level, intensity);
			}
		}

		// グリッドセルを描画（レベルが低い順に描画し、高いレベルが上に来るようにする）
		canvas.Save();
		try
		{
			foreach (var (cellKey, cellData) in gridCells.OrderBy(x => x.Value.level))
			{
				var (cellX, cellY) = cellKey;
				var level = cellData.level;

				// セルの左上座標を計算
				var cellLeft = originScreenX + (cellX - 0.5f) * GridCellSize;
				var cellTop = originScreenY + (cellY - 0.5f) * GridCellSize;

				var rect = new SKRect(cellLeft, cellTop, cellLeft + GridCellSize, cellTop + GridCellSize);

				var levelIndex = (int)level;
				if (levelIndex < 0 || levelIndex >= LevelBaseColors.Length)
					continue;

				var baseColor = LevelBaseColors[levelIndex];

				// アニメーションを適用した枠線のみ描画（塗りつぶしなし）
				var strokeAlpha = (byte)(80 + 140 * animationFactor);
				var strokeWidth = 1f + 2f * animationFactor;
				using var strokePaint = new SKPaint
				{
					Style = SKPaintStyle.Stroke,
					IsAntialias = true,
					StrokeWidth = strokeWidth,
					Color = baseColor.WithAlpha(strokeAlpha)
				};
				canvas.DrawRect(rect, strokePaint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	/// <summary>
	/// アニメーションモードに応じたアニメーション係数を計算する
	/// </summary>
	/// <param name="animationMode">アニメーションモード</param>
	/// <returns>0.0〜1.0のアニメーション係数</returns>
	private float CalculateAnimationFactor(ShakeDetectionAnimationMode animationMode)
	{
		return animationMode switch
		{
			ShakeDetectionAnimationMode.None => 1.0f,
			ShakeDetectionAnimationMode.Blink => _blinkState ? 1.0f : 0.3f,
			ShakeDetectionAnimationMode.Pulse => 0.3f + 0.7f * (float)(0.5 + 0.5 * Math.Sin(_animationStopwatch.Elapsed.TotalMilliseconds / PulseAnimationPeriodMs * 2 * Math.PI)),
			_ => 1.0f,
		};
	}

	private SKPath? GetOrCreatePath(KyoshinEvent evt)
	{
		// キャッシュチェック
		if (PathCache.TryGetValue(evt.Id, out var cached) && cached.PointCount == evt.PointCount)
			return cached.Path;

		// 古いキャッシュを破棄
		if (PathCache.TryGetValue(evt.Id, out var oldCache))
		{
			oldCache.Path.Dispose();
			PathCache.Remove(evt.Id);
		}

		var locations = evt.Points.Select(p => p.Location).ToList();
		SKPath? path = null;

		switch (locations.Count)
		{
			case 0:
				return null;

			case 1:
				// 1点の場合は円を描画
				path = PathGenerator.MakeCirclePath(locations[0], OffsetDistanceKm * 1000, BaseZoom);
				break;

			case 2:
				// 2点の場合はカプセル形状
				path = CreateCapsulePath(locations[0], locations[1], OffsetDistanceKm);
				break;

			default:
				// 3点以上の場合はオフセット付き凸包
				path = CreateConvexHullPath(locations, OffsetDistanceKm);
				break;
		}

		if (path != null)
			PathCache[evt.Id] = new CachedPath(evt.Id, evt.PointCount, path);

		return path;
	}

	private static SKPath CreateCapsulePath(Location p1, Location p2, double offsetKm)
	{
		var vertices = ConvexHullCalculator.CreateCapsuleVertices(p1, p2, offsetKm);
		return CreatePathFromVertices(vertices);
	}

	private static SKPath CreateConvexHullPath(List<Location> locations, double offsetKm)
	{
		var hull = ConvexHullCalculator.ComputeConvexHull(locations);
		if (hull.Count < 3)
		{
			// 凸包が作れない場合は最初の点を中心とした円を描画
			return PathGenerator.MakeCirclePath(locations[0], offsetKm * 1000, BaseZoom);
		}

		var offsetHull = ConvexHullCalculator.ApplyOffset(hull, offsetKm);
		return CreateRoundedPathFromVertices(offsetHull);
	}

	private static SKPath CreatePathFromVertices(List<Location> vertices)
	{
		var path = new SKPath();
		if (vertices.Count == 0)
			return path;

		var first = vertices[0].ToPixel(BaseZoom).AsSkPoint();
		path.MoveTo(first);

		for (var i = 1; i < vertices.Count; i++)
		{
			var point = vertices[i].ToPixel(BaseZoom).AsSkPoint();
			path.LineTo(point);
		}

		path.Close();
		return path;
	}

	/// <summary>
	/// 角を丸めたパスを生成（凸包用）
	/// </summary>
	private static SKPath CreateRoundedPathFromVertices(List<Location> vertices)
	{
		var path = new SKPath();
		if (vertices.Count < 3)
			return CreatePathFromVertices(vertices);

		// ピクセル座標に変換（基準ズームで）
		var points = vertices.Select(v => v.ToPixel(BaseZoom).AsSkPoint()).ToList();
		var count = points.Count;

		// 角丸の割合（0.0〜0.5、大きいほど丸くなる）
		const float roundRatio = 0.5f;

		// 最初の辺の中点から開始
		var startMid = Lerp(points[0], points[1], roundRatio);
		path.MoveTo(startMid);

		for (var i = 0; i < count; i++)
		{
			var curr = points[i];
			var next = points[(i + 1) % count];
			var nextNext = points[(i + 2) % count];

			// 辺の中点（丸めの開始点と終了点）
			var midCurrNext = Lerp(curr, next, 1 - roundRatio);
			var midNextNextNext = Lerp(next, nextNext, roundRatio);

			// 辺を直線で描画
			path.LineTo(midCurrNext);

			// 角を三次ベジェ曲線で丸める（より滑らかに）
			// 制御点を頂点に近い位置に2つ配置
			var ctrl1 = Lerp(midCurrNext, next, 0.5f);
			var ctrl2 = Lerp(next, midNextNextNext, 0.5f);
			path.CubicTo(ctrl1, ctrl2, midNextNextNext);
		}

		path.Close();
		return path;
	}

	private static SKPoint Lerp(SKPoint a, SKPoint b, float t)
		=> new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

	private void InvalidateCache()
	{
		foreach (var cached in PathCache.Values)
			cached.Path.Dispose();
		PathCache.Clear();
	}
}
