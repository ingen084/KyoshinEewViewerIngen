; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
KEVIJMA001 | JmaXml | Error | 別アセンブリで生成された読み取りの単位に対して、対象外のメンバーを参照することを禁止する (JmaXmlGenerator)
KEVIJMA002 | JmaXml | Error | [JmaXmlType] の型やメンバーの定義の誤りを検出する (JmaXmlGenerator)
KEVIJMA003 | JmaXml | Error | 読み取りコードを生成できない読み取りの単位を検出する (JmaXmlGenerator)
