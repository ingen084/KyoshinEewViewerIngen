namespace KyoshinEewViewer.BufrParser.Ixac41;

/// <summary>
/// 電文冒頭に格納される階級震度テーブルの1エントリ。凡例表示などに利用する想定。
/// </summary>
/// <param name="RangeQualifier">計測震度範囲修飾（0-08-193 の生値）。</param>
/// <param name="WeakStrongFlag">弱強修飾（0-08-198 の生値。0=無印、1=弱、2=強）。</param>
/// <param name="ClassCode">階級震度整数部（0-60-003 の生値）。</param>
/// <param name="LowerLimit">計測震度の下限値。</param>
/// <param name="UpperLimit">計測震度の上限値。</param>
public readonly record struct IntensityClassDefinition(int RangeQualifier, int WeakStrongFlag, int ClassCode, float LowerLimit, float UpperLimit);
