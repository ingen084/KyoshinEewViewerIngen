using KyoshinEewViewer.TravelTimeTable.Models;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.TravelTimeTable;

/// <summary>
/// 揺れ検知から震源要素を探索するエンジン
/// グリッドサーチと尤度最適化の2段階アルゴリズムを使用
/// </summary>
public class HypocenterSearchEngine
{
    private readonly TravelTimeCalculator _calculator;

    /// <summary>
    /// 探索パラメータ
    /// </summary>
    public HypocenterSearchParameters Parameters { get; set; } = HypocenterSearchParameters.Default;

    public HypocenterSearchEngine(TravelTimeCalculator calculator)
    {
        _calculator = calculator;
    }

    public HypocenterSearchEngine(TravelTimeTable travelTimeTable)
        : this(new TravelTimeCalculator(travelTimeTable))
    {
    }

    /// <summary>
    /// 検知点から震源要素を推定する
    /// </summary>
    /// <param name="detections">検知観測点のリスト</param>
    /// <returns>推定震源要素、推定できない場合はnull</returns>
    public EstimatedHypocenter? Search(IReadOnlyList<DetectionPoint> detections)
    {
        if (detections.Count < Parameters.MinStationCount)
            return null;

        // Phase 1: グリッドサーチで粗い推定
        var gridResult = PerformGridSearch(detections);
        if (gridResult == null)
            return null;

        // Phase 2: Nelder-Mead法で精密化
        var refinedResult = RefineWithNelderMead(detections, gridResult.Value);

        return refinedResult;
    }

    /// <summary>
    /// グリッドサーチによる粗い震源探索
    /// </summary>
    private (double Lat, double Lon, int Depth, DateTime OriginTime, double Score)? PerformGridSearch(
        IReadOnlyList<DetectionPoint> detections)
    {
        // 検知点の重心を探索中心とする
        var centerLat = detections.Average(d => d.Location.Latitude);
        var centerLon = detections.Average(d => d.Location.Longitude);

        // 最初の検知時刻を基準とする
        var firstDetection = detections.MinBy(d => d.DetectedAt);
        if (firstDetection.Location == null)
            return null;

        var baseTime = firstDetection.DetectedAt;

        double bestScore = double.MaxValue;
        double bestLat = centerLat;
        double bestLon = centerLon;
        int bestDepth = 10;
        DateTime bestOriginTime = baseTime;

        // グリッドサーチ
        for (var latOffset = -Parameters.GridSearchRangeDeg; latOffset <= Parameters.GridSearchRangeDeg; latOffset += Parameters.GridSearchStepDeg)
        {
            for (var lonOffset = -Parameters.GridSearchRangeDeg; lonOffset <= Parameters.GridSearchRangeDeg; lonOffset += Parameters.GridSearchStepDeg)
            {
                var lat = centerLat + latOffset;
                var lon = centerLon + lonOffset;

                for (var depth = Parameters.MinDepthKm; depth <= Parameters.MaxDepthKm; depth += Parameters.DepthStepKm)
                {
                    // この震央・深さに対する最適な発震時刻を推定
                    var estimatedOriginTime = EstimateOriginTime(detections, lat, lon, depth);
                    if (!estimatedOriginTime.HasValue)
                        continue;

                    // 残差スコアを計算
                    var score = CalculateResidualScore(detections, lat, lon, depth, estimatedOriginTime.Value);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestLat = lat;
                        bestLon = lon;
                        bestDepth = depth;
                        bestOriginTime = estimatedOriginTime.Value;
                    }
                }
            }
        }

        return bestScore < double.MaxValue
            ? (bestLat, bestLon, bestDepth, bestOriginTime, bestScore)
            : null;
    }

    /// <summary>
    /// Nelder-Mead法による震源の精密化
    /// </summary>
    private EstimatedHypocenter? RefineWithNelderMead(
        IReadOnlyList<DetectionPoint> detections,
        (double Lat, double Lon, int Depth, DateTime OriginTime, double Score) initial)
    {
        // 初期シンプレックスの生成
        var vertices = new List<(double Lat, double Lon, double Depth, double Score)>
        {
            (initial.Lat, initial.Lon, initial.Depth, initial.Score),
            (initial.Lat + Parameters.SimplexInitialSizeDeg, initial.Lon, initial.Depth, 0),
            (initial.Lat, initial.Lon + Parameters.SimplexInitialSizeDeg, initial.Depth, 0),
            (initial.Lat, initial.Lon, initial.Depth + Parameters.SimplexInitialSizeDepth, 0),
        };

        // 各頂点のスコアを計算
        for (var i = 1; i < vertices.Count; i++)
        {
            var v = vertices[i];
            var originTime = EstimateOriginTime(detections, v.Lat, v.Lon, (int)v.Depth);
            if (!originTime.HasValue)
            {
                vertices[i] = (v.Lat, v.Lon, v.Depth, double.MaxValue);
                continue;
            }
            var score = CalculateResidualScore(detections, v.Lat, v.Lon, (int)v.Depth, originTime.Value);
            vertices[i] = (v.Lat, v.Lon, v.Depth, score);
        }

        // Nelder-Mead反復
        for (var iter = 0; iter < Parameters.MaxIterations; iter++)
        {
            // スコア順にソート
            vertices = vertices.OrderBy(v => v.Score).ToList();

            var best = vertices[0];
            var worst = vertices[^1];
            var secondWorst = vertices[^2];

            // 収束判定
            var scoreRange = worst.Score - best.Score;
            if (scoreRange < Parameters.ConvergenceThreshold)
                break;

            // 重心を計算（最悪点を除く）
            var centroidLat = vertices.Take(vertices.Count - 1).Average(v => v.Lat);
            var centroidLon = vertices.Take(vertices.Count - 1).Average(v => v.Lon);
            var centroidDepth = vertices.Take(vertices.Count - 1).Average(v => v.Depth);

            // 反射
            var reflectLat = centroidLat + Parameters.ReflectionCoef * (centroidLat - worst.Lat);
            var reflectLon = centroidLon + Parameters.ReflectionCoef * (centroidLon - worst.Lon);
            var reflectDepth = Math.Clamp(centroidDepth + Parameters.ReflectionCoef * (centroidDepth - worst.Depth),
                Parameters.MinDepthKm, Parameters.MaxDepthKm);

            var reflectOriginTime = EstimateOriginTime(detections, reflectLat, reflectLon, (int)reflectDepth);
            var reflectScore = reflectOriginTime.HasValue
                ? CalculateResidualScore(detections, reflectLat, reflectLon, (int)reflectDepth, reflectOriginTime.Value)
                : double.MaxValue;

            if (reflectScore < best.Score)
            {
                // 拡大
                var expandLat = centroidLat + Parameters.ExpansionCoef * (reflectLat - centroidLat);
                var expandLon = centroidLon + Parameters.ExpansionCoef * (reflectLon - centroidLon);
                var expandDepth = Math.Clamp(centroidDepth + Parameters.ExpansionCoef * (reflectDepth - centroidDepth),
                    Parameters.MinDepthKm, Parameters.MaxDepthKm);

                var expandOriginTime = EstimateOriginTime(detections, expandLat, expandLon, (int)expandDepth);
                var expandScore = expandOriginTime.HasValue
                    ? CalculateResidualScore(detections, expandLat, expandLon, (int)expandDepth, expandOriginTime.Value)
                    : double.MaxValue;

                vertices[^1] = expandScore < reflectScore
                    ? (expandLat, expandLon, expandDepth, expandScore)
                    : (reflectLat, reflectLon, reflectDepth, reflectScore);
            }
            else if (reflectScore < secondWorst.Score)
            {
                vertices[^1] = (reflectLat, reflectLon, reflectDepth, reflectScore);
            }
            else
            {
                // 収縮
                var contractLat = centroidLat + Parameters.ContractionCoef * (worst.Lat - centroidLat);
                var contractLon = centroidLon + Parameters.ContractionCoef * (worst.Lon - centroidLon);
                var contractDepth = Math.Clamp(centroidDepth + Parameters.ContractionCoef * (worst.Depth - centroidDepth),
                    Parameters.MinDepthKm, Parameters.MaxDepthKm);

                var contractOriginTime = EstimateOriginTime(detections, contractLat, contractLon, (int)contractDepth);
                var contractScore = contractOriginTime.HasValue
                    ? CalculateResidualScore(detections, contractLat, contractLon, (int)contractDepth, contractOriginTime.Value)
                    : double.MaxValue;

                if (contractScore < worst.Score)
                {
                    vertices[^1] = (contractLat, contractLon, contractDepth, contractScore);
                }
                else
                {
                    // 縮小
                    for (var i = 1; i < vertices.Count; i++)
                    {
                        var v = vertices[i];
                        var shrinkLat = best.Lat + Parameters.ShrinkCoef * (v.Lat - best.Lat);
                        var shrinkLon = best.Lon + Parameters.ShrinkCoef * (v.Lon - best.Lon);
                        var shrinkDepth = Math.Clamp(best.Depth + Parameters.ShrinkCoef * (v.Depth - best.Depth),
                            Parameters.MinDepthKm, Parameters.MaxDepthKm);

                        var shrinkOriginTime = EstimateOriginTime(detections, shrinkLat, shrinkLon, (int)shrinkDepth);
                        var shrinkScore = shrinkOriginTime.HasValue
                            ? CalculateResidualScore(detections, shrinkLat, shrinkLon, (int)shrinkDepth, shrinkOriginTime.Value)
                            : double.MaxValue;

                        vertices[i] = (shrinkLat, shrinkLon, shrinkDepth, shrinkScore);
                    }
                }
            }
        }

        // 最良の結果を取得
        vertices = vertices.OrderBy(v => v.Score).ToList();
        var finalBest = vertices[0];

        if (finalBest.Score >= double.MaxValue)
            return null;

        var finalOriginTime = EstimateOriginTime(detections, finalBest.Lat, finalBest.Lon, (int)finalBest.Depth);
        if (!finalOriginTime.HasValue)
            return null;

        // 残差の標準偏差を計算
        var residuals = CalculateResiduals(detections, finalBest.Lat, finalBest.Lon, (int)finalBest.Depth, finalOriginTime.Value);
        var residualStdDev = residuals.Count > 1
            ? Math.Sqrt(residuals.Sum(r => r * r) / (residuals.Count - 1))
            : 0;

        // 信頼度スコアを計算（残差が小さいほど高い）
        var confidenceScore = Math.Exp(-residualStdDev / Parameters.ConfidenceScaleFactor);

        return new EstimatedHypocenter
        {
            Location = new Location((float)finalBest.Lat, (float)finalBest.Lon),
            DepthKm = (int)finalBest.Depth,
            OriginTime = finalOriginTime.Value,
            ConfidenceScore = Math.Clamp(confidenceScore, 0, 1),
            UsedStationCount = detections.Count,
            ResidualStdDev = residualStdDev,
        };
    }

    /// <summary>
    /// 検知点から発震時刻を推定する
    /// 各観測点からS波走時を逆算して中央値を取る
    /// </summary>
    private DateTime? EstimateOriginTime(
        IReadOnlyList<DetectionPoint> detections,
        double lat, double lon, int depth)
    {
        var originTimes = new List<DateTime>();

        foreach (var detection in detections)
        {
            var originTime = _calculator.EstimateOriginTimeFromSArrival(
                lat, lon, depth,
                detection.Location.Latitude, detection.Location.Longitude,
                detection.DetectedAt);

            if (originTime.HasValue)
                originTimes.Add(originTime.Value);
        }

        if (originTimes.Count == 0)
            return null;

        // 中央値を返す
        originTimes.Sort();
        var midIndex = originTimes.Count / 2;
        return originTimes.Count % 2 == 0
            ? originTimes[midIndex - 1].AddTicks((originTimes[midIndex].Ticks - originTimes[midIndex - 1].Ticks) / 2)
            : originTimes[midIndex];
    }

    /// <summary>
    /// 残差スコアを計算する（残差二乗和）
    /// </summary>
    private double CalculateResidualScore(
        IReadOnlyList<DetectionPoint> detections,
        double lat, double lon, int depth, DateTime originTime)
    {
        var residuals = CalculateResiduals(detections, lat, lon, depth, originTime);
        if (residuals.Count == 0)
            return double.MaxValue;

        return residuals.Sum(r => r * r);
    }

    /// <summary>
    /// 各観測点の残差（理論到達時刻 - 観測時刻）を計算する
    /// </summary>
    private List<double> CalculateResiduals(
        IReadOnlyList<DetectionPoint> detections,
        double lat, double lon, int depth, DateTime originTime)
    {
        var residuals = new List<double>();

        foreach (var detection in detections)
        {
            var theoreticalArrival = _calculator.CalculateSArrival(
                lat, lon, depth,
                detection.Location.Latitude, detection.Location.Longitude,
                originTime);

            if (!theoreticalArrival.HasValue)
                continue;

            var residual = (theoreticalArrival.Value - detection.DetectedAt).TotalSeconds;
            residuals.Add(residual);
        }

        return residuals;
    }

    /// <summary>
    /// 検知点が指定された震源要素と整合するかを判定する
    /// 同一イベント判定に使用
    /// </summary>
    /// <param name="detection">検知観測点</param>
    /// <param name="hypocenter">震源要素</param>
    /// <param name="toleranceSeconds">許容誤差（秒）</param>
    /// <returns>整合する場合はtrue</returns>
    public bool IsConsistent(DetectionPoint detection, EstimatedHypocenter hypocenter, double toleranceSeconds)
    {
        var theoreticalArrival = _calculator.CalculateSArrival(
            hypocenter.Location.Latitude, hypocenter.Location.Longitude, hypocenter.DepthKm,
            detection.Location.Latitude, detection.Location.Longitude,
            hypocenter.OriginTime);

        if (!theoreticalArrival.HasValue)
            return false;

        var residual = Math.Abs((theoreticalArrival.Value - detection.DetectedAt).TotalSeconds);
        return residual <= toleranceSeconds;
    }

    /// <summary>
    /// 複数の検知点が同一イベントに属するかを判定する
    /// </summary>
    /// <param name="detections">検知観測点のリスト</param>
    /// <param name="hypocenter">震源要素</param>
    /// <param name="toleranceSeconds">許容誤差（秒）</param>
    /// <returns>整合する検知点の割合（0.0-1.0）</returns>
    public double CalculateConsistencyRatio(
        IReadOnlyList<DetectionPoint> detections,
        EstimatedHypocenter hypocenter,
        double toleranceSeconds)
    {
        if (detections.Count == 0)
            return 0;

        var consistentCount = detections.Count(d => IsConsistent(d, hypocenter, toleranceSeconds));
        return (double)consistentCount / detections.Count;
    }
}

/// <summary>
/// 震源探索パラメータ
/// </summary>
public record class HypocenterSearchParameters
{
    /// <summary>
    /// 探索に必要な最小観測点数
    /// </summary>
    public int MinStationCount { get; init; } = 3;

    /// <summary>
    /// グリッドサーチの探索範囲（度）
    /// </summary>
    public double GridSearchRangeDeg { get; init; } = 2.0;

    /// <summary>
    /// グリッドサーチのステップ（度）
    /// </summary>
    public double GridSearchStepDeg { get; init; } = 0.2;

    /// <summary>
    /// 最小深さ (km)
    /// </summary>
    public int MinDepthKm { get; init; } = 0;

    /// <summary>
    /// 最大深さ (km)
    /// </summary>
    public int MaxDepthKm { get; init; } = 700;

    /// <summary>
    /// 深さのステップ (km)
    /// </summary>
    public int DepthStepKm { get; init; } = 10;

    /// <summary>
    /// Nelder-Mead法の最大反復回数
    /// </summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>
    /// 収束判定閾値
    /// </summary>
    public double ConvergenceThreshold { get; init; } = 0.01;

    /// <summary>
    /// 初期シンプレックスのサイズ（度）
    /// </summary>
    public double SimplexInitialSizeDeg { get; init; } = 0.1;

    /// <summary>
    /// 初期シンプレックスのサイズ（深さ km）
    /// </summary>
    public double SimplexInitialSizeDepth { get; init; } = 10;

    /// <summary>
    /// 反射係数
    /// </summary>
    public double ReflectionCoef { get; init; } = 1.0;

    /// <summary>
    /// 拡大係数
    /// </summary>
    public double ExpansionCoef { get; init; } = 2.0;

    /// <summary>
    /// 収縮係数
    /// </summary>
    public double ContractionCoef { get; init; } = 0.5;

    /// <summary>
    /// 縮小係数
    /// </summary>
    public double ShrinkCoef { get; init; } = 0.5;

    /// <summary>
    /// 信頼度計算のスケールファクター
    /// </summary>
    public double ConfidenceScaleFactor { get; init; } = 5.0;

    public static HypocenterSearchParameters Default { get; } = new();
}
