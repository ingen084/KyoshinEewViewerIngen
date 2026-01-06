namespace KyoshinEewViewer.TravelTimeTable;

/// <summary>
/// 走時表を使用してP波・S波の到達時刻を計算するクラス
/// </summary>
public class TravelTimeCalculator
{
    private readonly TravelTimeTable _travelTimeTable;

    public TravelTimeCalculator(TravelTimeTable travelTimeTable)
    {
        _travelTimeTable = travelTimeTable;
    }

    /// <summary>
    /// 震源から観測点へのP波到達時刻を計算する
    /// </summary>
    /// <param name="epicenterLat">震央緯度</param>
    /// <param name="epicenterLon">震央経度</param>
    /// <param name="depthKm">震源深さ (km)</param>
    /// <param name="stationLat">観測点緯度</param>
    /// <param name="stationLon">観測点経度</param>
    /// <param name="originTime">発震時刻</param>
    /// <returns>P波到達時刻、計算できない場合はnull</returns>
    public DateTime? CalculatePArrival(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon, DateTime originTime)
    {
        var pTravelTime = GetPTravelTime(epicenterLat, epicenterLon, depthKm, stationLat, stationLon);
        return pTravelTime.HasValue ? originTime.AddSeconds(pTravelTime.Value) : null;
    }

    /// <summary>
    /// 震源から観測点へのS波到達時刻を計算する
    /// </summary>
    /// <param name="epicenterLat">震央緯度</param>
    /// <param name="epicenterLon">震央経度</param>
    /// <param name="depthKm">震源深さ (km)</param>
    /// <param name="stationLat">観測点緯度</param>
    /// <param name="stationLon">観測点経度</param>
    /// <param name="originTime">発震時刻</param>
    /// <returns>S波到達時刻、計算できない場合はnull</returns>
    public DateTime? CalculateSArrival(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon, DateTime originTime)
    {
        var sTravelTime = GetSTravelTime(epicenterLat, epicenterLon, depthKm, stationLat, stationLon);
        return sTravelTime.HasValue ? originTime.AddSeconds(sTravelTime.Value) : null;
    }

    /// <summary>
    /// 震源から観測点へのP波・S波到達時刻を計算する
    /// </summary>
    public (DateTime PArrival, DateTime SArrival)? CalculateArrivals(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon, DateTime originTime)
    {
        var travelTimes = GetTravelTimes(epicenterLat, epicenterLon, depthKm, stationLat, stationLon);
        if (!travelTimes.HasValue)
            return null;

        return (
            originTime.AddSeconds(travelTimes.Value.PTime),
            originTime.AddSeconds(travelTimes.Value.STime));
    }

    /// <summary>
    /// 震源から観測点へのP波走時を取得する
    /// </summary>
    public double? GetPTravelTime(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon)
    {
        var distanceKm = CalculateEpicentralDistance(epicenterLat, epicenterLon, stationLat, stationLon);
        return _travelTimeTable.GetPTravelTime(depthKm, distanceKm);
    }

    /// <summary>
    /// 震源から観測点へのS波走時を取得する
    /// </summary>
    public double? GetSTravelTime(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon)
    {
        var distanceKm = CalculateEpicentralDistance(epicenterLat, epicenterLon, stationLat, stationLon);
        return _travelTimeTable.GetSTravelTime(depthKm, distanceKm);
    }

    /// <summary>
    /// 震源から観測点へのP波・S波走時を取得する
    /// </summary>
    public (double PTime, double STime)? GetTravelTimes(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon)
    {
        var distanceKm = CalculateEpicentralDistance(epicenterLat, epicenterLon, stationLat, stationLon);
        return _travelTimeTable.GetTravelTimes(depthKm, distanceKm);
    }

    /// <summary>
    /// 観測時刻とS波到達時刻から発震時刻を逆算する
    /// </summary>
    /// <param name="epicenterLat">震央緯度</param>
    /// <param name="epicenterLon">震央経度</param>
    /// <param name="depthKm">震源深さ (km)</param>
    /// <param name="stationLat">観測点緯度</param>
    /// <param name="stationLon">観測点経度</param>
    /// <param name="observedTime">観測時刻（S波到達とみなす）</param>
    /// <returns>推定発震時刻、計算できない場合はnull</returns>
    public DateTime? EstimateOriginTimeFromSArrival(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon, DateTime observedTime)
    {
        var sTravelTime = GetSTravelTime(epicenterLat, epicenterLon, depthKm, stationLat, stationLon);
        return sTravelTime.HasValue ? observedTime.AddSeconds(-sTravelTime.Value) : null;
    }

    /// <summary>
    /// 観測時刻とP波到達時刻から発震時刻を逆算する
    /// </summary>
    public DateTime? EstimateOriginTimeFromPArrival(
        double epicenterLat, double epicenterLon, int depthKm,
        double stationLat, double stationLon, DateTime observedTime)
    {
        var pTravelTime = GetPTravelTime(epicenterLat, epicenterLon, depthKm, stationLat, stationLon);
        return pTravelTime.HasValue ? observedTime.AddSeconds(-pTravelTime.Value) : null;
    }

    /// <summary>
    /// 2点間の震央距離を計算する (km)
    /// Hubeny式を使用
    /// </summary>
    public static double CalculateEpicentralDistance(
        double lat1, double lon1, double lat2, double lon2)
    {
        // Hubeny式による距離計算
        const double a = 6378137.0; // 赤道半径 (m)
        const double b = 6356752.314140; // 極半径 (m)
        const double e2 = (a * a - b * b) / (a * a); // 離心率の2乗

        var lat1Rad = lat1 * Math.PI / 180.0;
        var lat2Rad = lat2 * Math.PI / 180.0;
        var lon1Rad = lon1 * Math.PI / 180.0;
        var lon2Rad = lon2 * Math.PI / 180.0;

        var latDiff = lat1Rad - lat2Rad;
        var lonDiff = lon1Rad - lon2Rad;
        var latAvg = (lat1Rad + lat2Rad) / 2.0;

        var sinLat = Math.Sin(latAvg);
        var w = Math.Sqrt(1.0 - e2 * sinLat * sinLat);
        var m = a * (1.0 - e2) / (w * w * w); // 子午線曲率半径
        var n = a / w; // 卯酉線曲率半径

        var dx = lonDiff * n * Math.Cos(latAvg);
        var dy = latDiff * m;

        return Math.Sqrt(dx * dx + dy * dy) / 1000.0; // m -> km
    }

    /// <summary>
    /// 震源距離を計算する (km)
    /// 震央距離と深さから斜距離を計算
    /// </summary>
    public static double CalculateHypocentalDistance(
        double epicentralDistanceKm, double depthKm)
    {
        return Math.Sqrt(epicentralDistanceKm * epicentralDistanceKm + depthKm * depthKm);
    }
}
