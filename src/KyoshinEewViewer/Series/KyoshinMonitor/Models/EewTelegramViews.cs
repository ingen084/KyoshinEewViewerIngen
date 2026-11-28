using KyoshinEewViewer.JmaXmlParser;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// 緊急地震速報（地震動予報）の電文から読み取る項目
/// </summary>
[JmaXmlView]
internal partial struct EewForecastView;

/// <summary>
/// 緊急地震速報（警報）の電文から読み取る項目
/// </summary>
[JmaXmlView]
internal partial struct EewWarningView;

/// <summary>
/// リプレイファイルに含まれる緊急地震速報の電文から読み取る項目
/// </summary>
[JmaXmlView]
internal partial struct ReplayEewView;
