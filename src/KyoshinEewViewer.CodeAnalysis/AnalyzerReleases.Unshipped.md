; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
KEVI001 | Serialization | Error | ワークフローの Trigger/Action の Control 返却プロパティに [JsonIgnore] を強制する (WorkflowDisplayControlAnalyzer)
KEVIXAML001 | XamlBinding | Error | Command バインドされたメソッド名のオーバーロードを禁止する (XamlCommandBindingAnalyzer)
KEVIXAML002 | XamlBinding | Error | 値型 1引数メソッドへの CommandParameter 未指定/文字列リテラルの Command バインドを禁止する (XamlCommandBindingAnalyzer)
KEVIJMA001 | JmaXml | Error | 別アセンブリで生成された読み取りの単位に対して、対象外のメンバーを参照することを禁止する (JmaXmlGenerator)
KEVIJMA002 | JmaXml | Error | [JmaXmlType] の型やメンバーの定義の誤りを検出する (JmaXmlGenerator)
KEVIJMA003 | JmaXml | Error | 読み取りコードを生成できない読み取りの単位を検出する (JmaXmlGenerator)
