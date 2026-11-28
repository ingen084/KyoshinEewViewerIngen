namespace KyoshinEewViewer.JmaXmlParser.Tests;

/// <summary>
/// テストから参照するメンバーを読み取る
/// </summary>
[JmaXmlView]
internal partial struct TestView;

/// <summary>
/// プロパティパターンだけで参照するメンバーを読み取る
/// </summary>
[JmaXmlView]
internal partial struct PatternOnlyView;

/// <summary>
/// 電文のタイトルだけを読み取る。読み取り対象外のメンバーを参照した場合の検証に使う
/// </summary>
[JmaXmlView]
internal partial struct TitleOnlyView;
