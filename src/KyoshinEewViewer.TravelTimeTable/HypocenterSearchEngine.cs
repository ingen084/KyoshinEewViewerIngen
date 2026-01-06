using KyoshinEewViewer.TravelTimeTable.Models;
using KyoshinMonitorLib;
using System.Collections.Concurrent;
using System.Diagnostics;

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
        => Search(detections, null, null);

    /// <summary>
    /// 検知点から震源要素を推定する（未検知ペナルティ考慮版）
    /// </summary>
    /// <param name="detections">検知観測点のリスト</param>
    /// <param name="undetectedStations">未検知観測点のリスト（null可）</param>
    /// <param name="currentTime">現在時刻（null可）</param>
    /// <returns>推定震源要素、推定できない場合はnull</returns>
    public EstimatedHypocenter? Search(
        IReadOnlyList<DetectionPoint> detections,
        IReadOnlyList<UndetectedStation>? undetectedStations,
        DateTime? currentTime)
    {
        var totalStopwatch = Stopwatch.StartNew();

        if (detections.Count < Parameters.MinStationCount)
        {
            Debug.WriteLine($"[HypocenterSearch] 観測点数不足: {detections.Count} < {Parameters.MinStationCount}");
            return null;
        }

        Debug.WriteLine($"[HypocenterSearch] 開始: 観測点数={detections.Count}");

        // 未検知ペナルティを適用するか判定
        var firstDetection = detections.MinBy(d => d.DetectedAt);
        var shouldApplyUndetectedPenalty = false;
        if (undetectedStations != null && currentTime.HasValue)
        {
            var elapsedSeconds = (currentTime.Value - firstDetection.DetectedAt).TotalSeconds;
            // 揺れ検知から3秒以内、または10秒以内で揺れ検知数30点未満の場合
            shouldApplyUndetectedPenalty = elapsedSeconds <= 3 ||
                (elapsedSeconds <= 10 && detections.Count < 30);
        }

        // Phase 1: グリッドサーチで粗い推定
        var gridStopwatch = Stopwatch.StartNew();
        var gridResult = PerformGridSearch(detections, undetectedStations, currentTime, shouldApplyUndetectedPenalty);
        gridStopwatch.Stop();
        var gridSearchTimeMs = gridStopwatch.Elapsed.TotalMilliseconds;

        if (gridResult == null)
        {
            Debug.WriteLine($"[HypocenterSearch] グリッドサーチ失敗: {gridSearchTimeMs:F2}ms");
            return null;
        }

        Debug.WriteLine($"[HypocenterSearch] グリッドサーチ完了: {gridSearchTimeMs:F2}ms, 位置=({gridResult.Value.Lat:F2}, {gridResult.Value.Lon:F2}), 深さ={gridResult.Value.Depth}km, スコア={gridResult.Value.Score:F4}");

        // Phase 2: Nelder-Mead法で精密化
        var refineStopwatch = Stopwatch.StartNew();
        var refinedResult = RefineWithNelderMead(detections, gridResult.Value, gridSearchTimeMs, undetectedStations, currentTime, shouldApplyUndetectedPenalty);
        refineStopwatch.Stop();
        var refinementTimeMs = refineStopwatch.Elapsed.TotalMilliseconds;

        totalStopwatch.Stop();
        var totalTimeMs = totalStopwatch.Elapsed.TotalMilliseconds;

        if (refinedResult != null)
        {
            Debug.WriteLine($"[HypocenterSearch] 精密化完了: {refinementTimeMs:F2}ms, 位置=({refinedResult.Location.Latitude:F2}, {refinedResult.Location.Longitude:F2}), 深さ={refinedResult.DepthKm}km, 信頼度={refinedResult.ConfidenceScore:F2}");
            Debug.WriteLine($"[HypocenterSearch] 合計時間: {totalTimeMs:F2}ms (グリッド: {gridSearchTimeMs:F2}ms, 精密化: {refinementTimeMs:F2}ms)");
        }

        return refinedResult;
    }

    /// <summary>
    /// グリッドサーチによる粗い震源探索
    /// 並列化と観測点サンプリングにより高速化
    /// </summary>
    private (double Lat, double Lon, int Depth, DateTime OriginTime, double Score)? PerformGridSearch(
        IReadOnlyList<DetectionPoint> detections,
        IReadOnlyList<UndetectedStation>? undetectedStations = null,
        DateTime? currentTime = null,
        bool applyUndetectedPenalty = false)
    {
        // 検知点の重心を探索中心とする
        var centerLat = detections.Average(d => d.Location.Latitude);
        var centerLon = detections.Average(d => d.Location.Longitude);

        // 最初の検知時刻を基準とする
        var firstDetection = detections.MinBy(d => d.DetectedAt);
        if (firstDetection.Location == null)
            return null;

        var baseTime = firstDetection.DetectedAt;

        // グリッドサーチ用にサンプリング
        // 最初のN点と最も離れた観測点を優先的に選択
        var sampledDetections = SampleDetections(detections, Parameters.MaxGridSearchStations);

        // 未検知ペナルティ用: 検知観測点の最大震央距離を計算
        double maxDetectedDistance = 0;
        if (applyUndetectedPenalty)
        {
            foreach (var detection in detections)
            {
                var dist = TravelTimeCalculator.CalculateEpicentralDistance(
                    centerLat, centerLon,
                    detection.Location.Latitude, detection.Location.Longitude);
                if (dist > maxDetectedDistance)
                    maxDetectedDistance = dist;
            }
        }

        // グリッドポイントを事前に生成
        var gridPoints = new List<(double Lat, double Lon, int Depth)>();
        for (var latOffset = -Parameters.GridSearchRangeDeg; latOffset <= Parameters.GridSearchRangeDeg; latOffset += Parameters.GridSearchStepDeg)
        {
            for (var lonOffset = -Parameters.GridSearchRangeDeg; lonOffset <= Parameters.GridSearchRangeDeg; lonOffset += Parameters.GridSearchStepDeg)
            {
                var lat = centerLat + latOffset;
                var lon = centerLon + lonOffset;

                for (var depth = Parameters.MinDepthKm; depth <= Parameters.MaxDepthKm; depth += Parameters.DepthStepKm)
                {
                    gridPoints.Add((lat, lon, depth));
                }
            }
        }

        // 並列でグリッドサーチを実行
        var results = new ConcurrentBag<(double Lat, double Lon, int Depth, DateTime OriginTime, double Score)>();

        Parallel.ForEach(gridPoints, gridPoint =>
        {
            var (lat, lon, depth) = gridPoint;

            // この震央・深さに対する最適な発震時刻を推定
            var estimatedOriginTime = EstimateOriginTime(sampledDetections, lat, lon, depth);
            if (!estimatedOriginTime.HasValue)
                return;

            // 残差スコアを計算
            var rawScore = CalculateResidualScore(sampledDetections, lat, lon, depth, estimatedOriginTime.Value);
            if (rawScore >= double.MaxValue)
                return;

            // 深さペナルティを適用（深い震源ほどスコアを増加させて優先度を下げる）
            var depthPenalty = depth * Parameters.DepthPenaltyFactor;
            var score = rawScore + depthPenalty;

            // 未検知ペナルティを適用
            if (applyUndetectedPenalty && undetectedStations != null && currentTime.HasValue)
            {
                var undetectedPenalty = CalculateUndetectedPenalty(
                    lat, lon, depth, estimatedOriginTime.Value,
                    undetectedStations, currentTime.Value, maxDetectedDistance);
                score += undetectedPenalty;
            }

            results.Add((lat, lon, depth, estimatedOriginTime.Value, score));
        });

        if (results.IsEmpty)
            return null;

        // 最良の結果を取得
        var best = results.MinBy(r => r.Score);
        return best;
    }

    /// <summary>
    /// 検知点をサンプリングする
    /// 初期検知点と空間的に分散した観測点を優先的に選択
    /// </summary>
    private static IReadOnlyList<DetectionPoint> SampleDetections(IReadOnlyList<DetectionPoint> detections, int maxCount)
    {
        if (detections.Count <= maxCount)
            return detections;

        // 時刻順にソート
        var sorted = detections.OrderBy(d => d.DetectedAt).ToList();

        // 最初の半分は時刻順で選択（初動が重要）
        var halfCount = maxCount / 2;
        var selected = sorted.Take(halfCount).ToList();
        var remaining = sorted.Skip(halfCount).ToList();

        // 残りは空間的に分散するように選択
        while (selected.Count < maxCount && remaining.Count > 0)
        {
            // 既選択点から最も離れた点を選択
            var bestIndex = -1;
            double maxMinDistance = 0;

            for (var i = 0; i < remaining.Count; i++)
            {
                var candidate = remaining[i];
                var minDistance = selected.Min(s =>
                    TravelTimeCalculator.CalculateEpicentralDistance(
                        s.Location.Latitude, s.Location.Longitude,
                        candidate.Location.Latitude, candidate.Location.Longitude));

                if (minDistance > maxMinDistance)
                {
                    maxMinDistance = minDistance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                selected.Add(remaining[bestIndex]);
                remaining.RemoveAt(bestIndex);
            }
            else
            {
                break;
            }
        }

        return selected;
    }

    /// <summary>
    /// Nelder-Mead法による震源の精密化
    /// 精密化時は深さペナルティを適用しない（純粋な残差で評価）
    /// </summary>
    private EstimatedHypocenter? RefineWithNelderMead(
        IReadOnlyList<DetectionPoint> detections,
        (double Lat, double Lon, int Depth, DateTime OriginTime, double Score) initial,
        double gridSearchTimeMs = 0,
        IReadOnlyList<UndetectedStation>? undetectedStations = null,
        DateTime? currentTime = null,
        bool applyUndetectedPenalty = false)
    {
        var refinementStopwatch = Stopwatch.StartNew();

        // 未検知ペナルティ用: 検知観測点の最大震央距離を計算
        double maxDetectedDistance = 0;
        if (applyUndetectedPenalty)
        {
            foreach (var detection in detections)
            {
                var dist = TravelTimeCalculator.CalculateEpicentralDistance(
                    initial.Lat, initial.Lon,
                    detection.Location.Latitude, detection.Location.Longitude);
                if (dist > maxDetectedDistance)
                    maxDetectedDistance = dist;
            }
        }

        // スコア計算用のローカル関数
        double CalcScore(double lat, double lon, double depth)
        {
            var score = CalculateRefinementScore(detections, lat, lon, depth);
            if (applyUndetectedPenalty && undetectedStations != null && currentTime.HasValue && score < double.MaxValue)
            {
                var originTime = EstimateOriginTime(detections, lat, lon, (int)depth);
                if (originTime.HasValue)
                {
                    score += CalculateUndetectedPenalty(
                        lat, lon, (int)depth, originTime.Value,
                        undetectedStations, currentTime.Value, maxDetectedDistance);
                }
            }
            return score;
        }

        // 初期シンプレックスの生成
        var vertices = new List<(double Lat, double Lon, double Depth, double Score)>
        {
            (initial.Lat, initial.Lon, initial.Depth, CalcScore(initial.Lat, initial.Lon, initial.Depth)),
            (initial.Lat + Parameters.SimplexInitialSizeDeg, initial.Lon, initial.Depth, 0),
            (initial.Lat, initial.Lon + Parameters.SimplexInitialSizeDeg, initial.Depth, 0),
            (initial.Lat, initial.Lon, initial.Depth + Parameters.SimplexInitialSizeDepth, 0),
        };

        // 各頂点のスコアを計算
        for (var i = 1; i < vertices.Count; i++)
        {
            var v = vertices[i];
            var clampedDepth = Math.Clamp(v.Depth, Parameters.MinDepthKm, Parameters.MaxDepthKm);
            var score = CalcScore(v.Lat, v.Lon, clampedDepth);
            vertices[i] = (v.Lat, v.Lon, clampedDepth, score);
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

            var reflectScore = CalcScore(reflectLat, reflectLon, reflectDepth);

            if (reflectScore < best.Score)
            {
                // 拡大
                var expandLat = centroidLat + Parameters.ExpansionCoef * (reflectLat - centroidLat);
                var expandLon = centroidLon + Parameters.ExpansionCoef * (reflectLon - centroidLon);
                var expandDepth = Math.Clamp(centroidDepth + Parameters.ExpansionCoef * (reflectDepth - centroidDepth),
                    Parameters.MinDepthKm, Parameters.MaxDepthKm);

                var expandScore = CalcScore(expandLat, expandLon, expandDepth);

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

                var contractScore = CalcScore(contractLat, contractLon, contractDepth);

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

                        var shrinkScore = CalcScore(shrinkLat, shrinkLon, shrinkDepth);

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

        // 結果を0.1度単位・10km単位に丸める
        var roundedLat = Math.Round(finalBest.Lat, 1);
        var roundedLon = Math.Round(finalBest.Lon, 1);
        var roundedDepth = (int)(Math.Round(finalBest.Depth / 10.0) * 10);
        roundedDepth = Math.Clamp(roundedDepth, Parameters.MinDepthKm, Parameters.MaxDepthKm);

        var finalOriginTime = EstimateOriginTime(detections, roundedLat, roundedLon, roundedDepth);
        if (!finalOriginTime.HasValue)
            return null;

        // 残差の標準偏差を計算
        var residuals = CalculateResiduals(detections, roundedLat, roundedLon, roundedDepth, finalOriginTime.Value);
        var residualStdDev = residuals.Count > 1
            ? Math.Sqrt(residuals.Sum(r => r * r) / (residuals.Count - 1))
            : 0;

        // 信頼度スコアを計算（残差が小さいほど高い）
        var confidenceScore = Math.Exp(-residualStdDev / Parameters.ConfidenceScaleFactor);

        refinementStopwatch.Stop();
        var refinementTimeMs = refinementStopwatch.Elapsed.TotalMilliseconds;
        var totalTimeMs = gridSearchTimeMs + refinementTimeMs;

        return new EstimatedHypocenter
        {
            Location = new Location((float)roundedLat, (float)roundedLon),
            DepthKm = roundedDepth,
            OriginTime = finalOriginTime.Value,
            ConfidenceScore = Math.Clamp(confidenceScore, 0, 1),
            UsedStationCount = detections.Count,
            ResidualStdDev = residualStdDev,
            CalculationTimeMs = totalTimeMs,
            GridSearchTimeMs = gridSearchTimeMs,
            RefinementTimeMs = refinementTimeMs,
        };
    }

    /// <summary>
    /// 検知点から発震時刻を推定する
    /// 各観測点からP波またはS波走時を逆算して中央値を取る
    /// </summary>
    private DateTime? EstimateOriginTime(
        IReadOnlyList<DetectionPoint> detections,
        double lat, double lon, int depth)
    {
        var originTimes = new List<DateTime>();

        foreach (var detection in detections)
        {
            // P波とS波の両方から発震時刻を推定
            var originTimeFromS = _calculator.EstimateOriginTimeFromSArrival(
                lat, lon, depth,
                detection.Location.Latitude, detection.Location.Longitude,
                detection.DetectedAt);

            var originTimeFromP = _calculator.EstimateOriginTimeFromPArrival(
                lat, lon, depth,
                detection.Location.Latitude, detection.Location.Longitude,
                detection.DetectedAt);

            // S波による推定を優先（一般的にS波で検知することが多い）
            if (originTimeFromS.HasValue)
                originTimes.Add(originTimeFromS.Value);
            else if (originTimeFromP.HasValue)
                originTimes.Add(originTimeFromP.Value);
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
    /// 精密化フェーズ用のスコアを計算する
    /// 発震時刻を推定してから残差スコアを計算
    /// </summary>
    private double CalculateRefinementScore(
        IReadOnlyList<DetectionPoint> detections,
        double lat, double lon, double depth)
    {
        var clampedDepth = (int)Math.Clamp(depth, Parameters.MinDepthKm, Parameters.MaxDepthKm);
        var originTime = EstimateOriginTime(detections, lat, lon, clampedDepth);
        if (!originTime.HasValue)
            return double.MaxValue;

        return CalculateResidualScore(detections, lat, lon, clampedDepth, originTime.Value);
    }

    /// <summary>
    /// 各観測点の残差（理論到達時刻 - 観測時刻）を計算する
    /// P波とS波の両方を考慮し、残差が小さい方を採用する
    /// </summary>
    private List<double> CalculateResiduals(
        IReadOnlyList<DetectionPoint> detections,
        double lat, double lon, int depth, DateTime originTime)
    {
        var residuals = new List<double>();

        foreach (var detection in detections)
        {
            var theoreticalPArrival = _calculator.CalculatePArrival(
                lat, lon, depth,
                detection.Location.Latitude, detection.Location.Longitude,
                originTime);

            var theoreticalSArrival = _calculator.CalculateSArrival(
                lat, lon, depth,
                detection.Location.Latitude, detection.Location.Longitude,
                originTime);

            // P波とS波の両方の残差を計算し、絶対値が小さい方を採用
            double? residual = null;

            if (theoreticalPArrival.HasValue)
            {
                var pResidual = (theoreticalPArrival.Value - detection.DetectedAt).TotalSeconds;
                residual = pResidual;
            }

            if (theoreticalSArrival.HasValue)
            {
                var sResidual = (theoreticalSArrival.Value - detection.DetectedAt).TotalSeconds;
                if (!residual.HasValue || Math.Abs(sResidual) < Math.Abs(residual.Value))
                    residual = sResidual;
            }

            if (residual.HasValue)
                residuals.Add(residual.Value);
        }

        return residuals;
    }

    /// <summary>
    /// 未検知ペナルティを計算する
    /// 理論上は揺れが到達済みのはずなのに未検知の観測点がある場合、ペナルティを加算する
    /// </summary>
    /// <param name="lat">震央緯度</param>
    /// <param name="lon">震央経度</param>
    /// <param name="depth">震源深さ (km)</param>
    /// <param name="originTime">発震時刻</param>
    /// <param name="undetectedStations">未検知観測点のリスト</param>
    /// <param name="currentTime">現在時刻</param>
    /// <param name="maxDetectedDistance">検知観測点の最大震央距離 (km)</param>
    /// <returns>未検知ペナルティスコア</returns>
    private double CalculateUndetectedPenalty(
        double lat, double lon, int depth, DateTime originTime,
        IReadOnlyList<UndetectedStation> undetectedStations,
        DateTime currentTime,
        double maxDetectedDistance)
    {
        var penalty = 0.0;
        var searchRadiusKm = maxDetectedDistance + 30; // 最大震央距離+30km以内

        foreach (var station in undetectedStations)
        {
            // 震央距離を計算
            var distanceKm = TravelTimeCalculator.CalculateEpicentralDistance(
                lat, lon,
                station.Location.Latitude, station.Location.Longitude);

            // 最大震央距離+30km以内の観測点のみ対象
            if (distanceKm > searchRadiusKm)
                continue;

            // P波とS波の理論到達時刻を計算
            var theoreticalPArrival = _calculator.CalculatePArrival(
                lat, lon, depth,
                station.Location.Latitude, station.Location.Longitude,
                originTime);

            var theoreticalSArrival = _calculator.CalculateSArrival(
                lat, lon, depth,
                station.Location.Latitude, station.Location.Longitude,
                originTime);

            // S波到達時刻より現在時刻が後ろなら到達済みとみなす（検知されるべき）
            // S波の方が揺れが大きいため、S波基準で判定
            if (theoreticalSArrival.HasValue && currentTime > theoreticalSArrival.Value)
            {
                // 到達済みのはずなのに未検知 → ペナルティを加算
                penalty += Parameters.UndetectedPenaltyFactor;
            }
        }

        return penalty;
    }

    /// <summary>
    /// 検知点が指定された震源要素と整合するかを判定する
    /// P波またはS波のどちらかで整合すればtrue
    /// 同一イベント判定に使用
    /// </summary>
    /// <param name="detection">検知観測点</param>
    /// <param name="hypocenter">震源要素</param>
    /// <param name="toleranceSeconds">許容誤差（秒）</param>
    /// <returns>整合する場合はtrue</returns>
    public bool IsConsistent(DetectionPoint detection, EstimatedHypocenter hypocenter, double toleranceSeconds)
    {
        var theoreticalPArrival = _calculator.CalculatePArrival(
            hypocenter.Location.Latitude, hypocenter.Location.Longitude, hypocenter.DepthKm,
            detection.Location.Latitude, detection.Location.Longitude,
            hypocenter.OriginTime);

        var theoreticalSArrival = _calculator.CalculateSArrival(
            hypocenter.Location.Latitude, hypocenter.Location.Longitude, hypocenter.DepthKm,
            detection.Location.Latitude, detection.Location.Longitude,
            hypocenter.OriginTime);

        // P波またはS波のどちらかで許容誤差内なら整合とみなす
        if (theoreticalPArrival.HasValue)
        {
            var pResidual = Math.Abs((theoreticalPArrival.Value - detection.DetectedAt).TotalSeconds);
            if (pResidual <= toleranceSeconds)
                return true;
        }

        if (theoreticalSArrival.HasValue)
        {
            var sResidual = Math.Abs((theoreticalSArrival.Value - detection.DetectedAt).TotalSeconds);
            if (sResidual <= toleranceSeconds)
                return true;
        }

        return false;
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
    public double GridSearchStepDeg { get; init; } = 0.1;

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
    /// 深さペナルティ係数
    /// 深い震源ほどスコアにペナルティを加えて浅い解を優先する
    /// </summary>
    public double DepthPenaltyFactor { get; init; } = 0.1;

    /// <summary>
    /// グリッドサーチで使用する最大観測点数
    /// 計算時間短縮のため、初期グリッドサーチでは一部の観測点のみ使用
    /// </summary>
    public int MaxGridSearchStations { get; init; } = 50;

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

    /// <summary>
    /// 未検知ペナルティ係数
    /// 到達済みのはずなのに未検知の観測点1つあたりに加算するペナルティ
    /// </summary>
    public double UndetectedPenaltyFactor { get; init; } = 1.0;

    public static HypocenterSearchParameters Default { get; } = new();
}
