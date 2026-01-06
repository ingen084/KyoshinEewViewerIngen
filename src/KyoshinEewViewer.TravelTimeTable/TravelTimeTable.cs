using KyoshinEewViewer.TravelTimeTable.Models;
using MessagePack;

namespace KyoshinEewViewer.TravelTimeTable;

/// <summary>
/// 走時表データを管理し、補間処理を行うクラス
/// tjma2001走時表を使用してP波・S波の走時を取得する
/// </summary>
public class TravelTimeTable
{
    /// <summary>
    /// 走時表エントリのリスト
    /// </summary>
    private readonly TravelTimeEntry[] _entries;

    /// <summary>
    /// 深さ別・距離別のルックアップテーブル
    /// Key: (DepthKm, DistanceKm)
    /// </summary>
    private readonly Dictionary<(int Depth, int Distance), TravelTimeEntry> _lookup;

    /// <summary>
    /// 利用可能な深さのリスト（ソート済み）
    /// </summary>
    public int[] AvailableDepths { get; }

    /// <summary>
    /// 利用可能な距離のリスト（ソート済み）
    /// </summary>
    public int[] AvailableDistances { get; }

    /// <summary>
    /// 最大深さ (km)
    /// </summary>
    public int MaxDepth { get; }

    /// <summary>
    /// 最大距離 (km)
    /// </summary>
    public int MaxDistance { get; }

    private TravelTimeTable(TravelTimeEntry[] entries)
    {
        _entries = entries;
        _lookup = entries.ToDictionary(e => (e.DepthKm, e.DistanceKm));

        AvailableDepths = entries.Select(e => e.DepthKm).Distinct().OrderBy(d => d).ToArray();
        AvailableDistances = entries.Select(e => e.DistanceKm).Distinct().OrderBy(d => d).ToArray();

        MaxDepth = AvailableDepths.Length > 0 ? AvailableDepths[^1] : 0;
        MaxDistance = AvailableDistances.Length > 0 ? AvailableDistances[^1] : 0;
    }

    /// <summary>
    /// MessagePack形式のファイルから走時表を読み込む
    /// </summary>
    public static async Task<TravelTimeTable> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var entries = await MessagePackSerializer.DeserializeAsync<TravelTimeEntry[]>(
            stream,
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray),
            cancellationToken);

        return new TravelTimeTable(entries ?? []);
    }

    /// <summary>
    /// MessagePack形式のストリームから走時表を読み込む
    /// </summary>
    public static async Task<TravelTimeTable> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var entries = await MessagePackSerializer.DeserializeAsync<TravelTimeEntry[]>(
            stream,
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray),
            cancellationToken);

        return new TravelTimeTable(entries ?? []);
    }

    /// <summary>
    /// 走時表エントリの配列から走時表を作成する
    /// </summary>
    public static TravelTimeTable FromEntries(TravelTimeEntry[] entries)
        => new(entries);

    /// <summary>
    /// 指定された深さ・距離のP波走時を取得する（補間あり）
    /// </summary>
    /// <param name="depthKm">震源深さ (km)</param>
    /// <param name="distanceKm">震央距離 (km)</param>
    /// <returns>P波走時（秒）、範囲外の場合はnull</returns>
    public double? GetPTravelTime(double depthKm, double distanceKm)
    {
        var result = GetInterpolatedEntry(depthKm, distanceKm);
        return result?.PTimeSeconds;
    }

    /// <summary>
    /// 指定された深さ・距離のS波走時を取得する（補間あり）
    /// </summary>
    /// <param name="depthKm">震源深さ (km)</param>
    /// <param name="distanceKm">震央距離 (km)</param>
    /// <returns>S波走時（秒）、範囲外の場合はnull</returns>
    public double? GetSTravelTime(double depthKm, double distanceKm)
    {
        var result = GetInterpolatedEntry(depthKm, distanceKm);
        return result?.STimeSeconds;
    }

    /// <summary>
    /// 指定された深さ・距離のP波・S波走時を取得する（補間あり）
    /// </summary>
    /// <param name="depthKm">震源深さ (km)</param>
    /// <param name="distanceKm">震央距離 (km)</param>
    /// <returns>(P波走時, S波走時)のタプル（秒）、範囲外の場合はnull</returns>
    public (double PTime, double STime)? GetTravelTimes(double depthKm, double distanceKm)
    {
        var result = GetInterpolatedEntry(depthKm, distanceKm);
        if (result == null)
            return null;
        return (result.Value.PTimeSeconds, result.Value.STimeSeconds);
    }

    /// <summary>
    /// 双線形補間を使用して走時を計算する
    /// </summary>
    private TravelTimeEntry? GetInterpolatedEntry(double depthKm, double distanceKm)
    {
        if (AvailableDepths.Length == 0 || AvailableDistances.Length == 0)
            return null;

        // 範囲外チェック
        if (depthKm < 0 || distanceKm < 0)
            return null;

        // 深さの上下インデックスを取得
        var (depthLow, depthHigh, depthRatio) = FindBoundingIndices(AvailableDepths, depthKm);
        if (depthLow < 0)
            return null;

        // 距離の上下インデックスを取得
        var (distLow, distHigh, distRatio) = FindBoundingIndices(AvailableDistances, distanceKm);
        if (distLow < 0)
            return null;

        // 4点の走時を取得
        if (!TryGetEntry(AvailableDepths[depthLow], AvailableDistances[distLow], out var e00) ||
            !TryGetEntry(AvailableDepths[depthLow], AvailableDistances[distHigh], out var e01) ||
            !TryGetEntry(AvailableDepths[depthHigh], AvailableDistances[distLow], out var e10) ||
            !TryGetEntry(AvailableDepths[depthHigh], AvailableDistances[distHigh], out var e11))
        {
            // 補間できない場合は最も近い点を返す
            return GetNearestEntry(depthKm, distanceKm);
        }

        // 双線形補間
        var pTime = BilinearInterpolate(e00.PTimeMs, e01.PTimeMs, e10.PTimeMs, e11.PTimeMs, distRatio, depthRatio);
        var sTime = BilinearInterpolate(e00.STimeMs, e01.STimeMs, e10.STimeMs, e11.STimeMs, distRatio, depthRatio);

        return new TravelTimeEntry(
            (int)Math.Round(distanceKm),
            (int)Math.Round(depthKm),
            (int)Math.Round(pTime),
            (int)Math.Round(sTime));
    }

    /// <summary>
    /// 値を挟む上下のインデックスと補間比率を取得する
    /// </summary>
    private static (int LowIndex, int HighIndex, double Ratio) FindBoundingIndices(int[] sortedValues, double value)
    {
        if (sortedValues.Length == 0)
            return (-1, -1, 0);

        // 最小値より小さい場合
        if (value < sortedValues[0])
            return (0, 0, 0);

        // 最大値より大きい場合
        if (value >= sortedValues[^1])
            return (sortedValues.Length - 1, sortedValues.Length - 1, 0);

        // 二分探索で位置を特定
        var index = Array.BinarySearch(sortedValues, (int)value);
        if (index >= 0)
        {
            // 完全一致
            return (index, index, 0);
        }

        // 挿入位置から上下インデックスを計算
        var insertIndex = ~index;
        var lowIndex = insertIndex - 1;
        var highIndex = insertIndex;

        if (lowIndex < 0)
            lowIndex = 0;
        if (highIndex >= sortedValues.Length)
            highIndex = sortedValues.Length - 1;

        // 補間比率を計算
        var low = sortedValues[lowIndex];
        var high = sortedValues[highIndex];
        var ratio = low == high ? 0 : (value - low) / (high - low);

        return (lowIndex, highIndex, ratio);
    }

    /// <summary>
    /// 双線形補間を実行
    /// </summary>
    private static double BilinearInterpolate(double v00, double v01, double v10, double v11, double xRatio, double yRatio)
    {
        var v0 = v00 + (v01 - v00) * xRatio;
        var v1 = v10 + (v11 - v10) * xRatio;
        return v0 + (v1 - v0) * yRatio;
    }

    /// <summary>
    /// 指定された深さ・距離に最も近いエントリを取得する
    /// </summary>
    private TravelTimeEntry? GetNearestEntry(double depthKm, double distanceKm)
    {
        if (_entries.Length == 0)
            return null;

        return _entries.MinBy(e =>
            Math.Pow(e.DepthKm - depthKm, 2) + Math.Pow(e.DistanceKm - distanceKm, 2));
    }

    private bool TryGetEntry(int depthKm, int distanceKm, out TravelTimeEntry entry)
        => _lookup.TryGetValue((depthKm, distanceKm), out entry);
}
