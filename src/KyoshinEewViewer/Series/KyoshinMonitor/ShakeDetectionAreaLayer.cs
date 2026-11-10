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
	/// 最も内側（最上位レベル）の範囲の、観測点からの距離（km）
	/// </summary>
	private const double InnermostOffsetKm = 20.0;

	/// <summary>
	/// 1つ外側のレベルの範囲へ移るごとに広げる距離（km）
	/// </summary>
	private const double OffsetStepKm = 5.0;

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

	private record LevelPath(KyoshinEventLevel Level, SKPath Path);
	private record CachedPaths(int PointCount, LevelPath[] Paths);
	private Dictionary<Guid, CachedPaths> PathCache { get; } = [];

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
		// EEW発表中の場合は描画しない
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

			result.AddRange(SnapshotPoints(evt));
		}

		return result;
	}

	/// <summary>
	/// 揺れ検知の処理と並行して変更されうる観測点リストのスナップショットを取得する
	/// </summary>
	private static RealtimeObservationPoint[] SnapshotPoints(KyoshinEvent evt)
	{
		try
		{
			return evt.Points.Where(p => p != null).ToArray();
		}
		catch (InvalidOperationException)
		{
			// コレクションが変更された場合はこのイベントをスキップ
			return [];
		}
	}

	private void RenderConvexHull(SKCanvas canvas, LayerRenderParameter param, ShakeDetectionAnimationMode animationMode)
	{
		var events = KyoshinEvents;
		if (events == null)
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

			var levelPaths = new List<LevelPath>();
			foreach (var evt in events)
			{
				if (!evt.IsConfirmed || evt.PointCount == 0)
					continue;

				levelPaths.AddRange(GetOrCreateLevelPaths(evt));
			}

			// 上位レベルの線が上に重なるよう、レベルの低い順に描画する
			foreach (var (level, path) in levelPaths.OrderBy(p => p.Level))
			{
				var levelIndex = (int)level;
				if (levelIndex < 0 || levelIndex >= LevelBaseColors.Length)
					continue;

				// アニメーションを適用した枠線のみ描画（塗りつぶしなし）
				var baseColor = LevelBaseColors[levelIndex];
				var baseStrokeWidth = LevelStrokeWidths[levelIndex];
				var animatedAlpha = (byte)(80 + 140 * animationFactor);
				var animatedStrokeWidth = baseStrokeWidth * (0.5f + 0.5f * animationFactor);

				// スケールに反比例させて画面上で一定の太さを保つ
				using var strokePaint = new SKPaint
				{
					Style = SKPaintStyle.Stroke,
					IsAntialias = true,
					StrokeWidth = animatedStrokeWidth / scale,
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

	private LevelPath[] GetOrCreateLevelPaths(KyoshinEvent evt)
	{
		if (PathCache.TryGetValue(evt.Id, out var cached))
		{
			if (cached.PointCount == evt.PointCount)
				return cached.Paths;

			DisposePaths(cached);
			PathCache.Remove(evt.Id);
		}

		var pointCount = evt.PointCount;
		var points = SnapshotPoints(evt);
		if (points.Length == 0)
			return [];

		var paths = CreateLevelPaths(evt.Level, points);
		PathCache[evt.Id] = new CachedPaths(pointCount, paths);
		return paths;
	}

	/// <summary>
	/// 観測点のレベルごとに、そのレベル以上の観測点を囲む範囲のパスを生成する。
	/// 下位レベルの範囲ほど観測点から離して囲み、上位レベルの範囲を内側に含める
	/// </summary>
	private static LevelPath[] CreateLevelPaths(KyoshinEventLevel eventLevel, RealtimeObservationPoint[] points)
	{
		// 単独の観測点のノイズを範囲として出さないよう、KyoshinEvent.AddPoint の規則で上がったイベントのレベルで頭打ちにする
		var leveledPoints = points
			.Select(p =>
			{
				var level = KyoshinEvent.GetLevel(p.LatestIntensity);
				return (p.Location, Level: level < eventLevel ? level : eventLevel);
			})
			.ToArray();
		var levels = leveledPoints
			.Select(p => p.Level)
			.Distinct()
			.OrderByDescending(l => l)
			.ToArray();

		var result = new LevelPath[levels.Length];
		for (var i = 0; i < levels.Length; i++)
		{
			var level = levels[i];
			var locations = leveledPoints.Where(p => p.Level >= level).Select(p => p.Location);
			result[i] = new LevelPath(level, CreateRoundedHullPath(locations, InnermostOffsetKm + OffsetStepKm * i));
		}
		return result;
	}

	private static SKPath CreateRoundedHullPath(IEnumerable<Location> locations, double offsetKm)
	{
		var hull = ConvexHullCalculator.ComputeConvexHull(locations);
		var vertices = ConvexHullCalculator.CreateRoundedOffsetPolygon(hull, offsetKm);

		var path = new SKPath();
		if (vertices.Count == 0)
			return path;

		path.MoveTo(vertices[0].ToPixel(BaseZoom).AsSkPoint());
		for (var i = 1; i < vertices.Count; i++)
			path.LineTo(vertices[i].ToPixel(BaseZoom).AsSkPoint());
		path.Close();
		return path;
	}

	private static void DisposePaths(CachedPaths cached)
	{
		foreach (var levelPath in cached.Paths)
			levelPath.Path.Dispose();
	}

	private void InvalidateCache()
	{
		foreach (var cached in PathCache.Values)
			DisposePaths(cached);
		PathCache.Clear();
	}
}
