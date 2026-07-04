namespace KyoshinEewViewer.BufrParser.Ixac41;

/// <summary>
/// 推計震度分布図（IXAC41）の1メッシュ分の計算結果。<see cref="SouthWestLatitude"/>/<see cref="SouthWestLongitude"/> は
/// 4分の1地域メッシュ（約250m四方）の南西端座標を表す。
/// </summary>
public readonly record struct EstimatedIntensityMesh(double SouthWestLatitude, double SouthWestLongitude, float CalculatedIntensity);
