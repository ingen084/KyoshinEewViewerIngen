; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
KEVI001 | Serialization | Error | ワークフローの Trigger/Action の Control 返却プロパティに [JsonIgnore] を強制する (WorkflowDisplayControlAnalyzer)
KEVIXAML001 | XamlBinding | Error | Command バインドされたメソッド名のオーバーロードを禁止する (XamlCommandBindingAnalyzer)
KEVIXAML002 | XamlBinding | Error | 値型 1引数メソッドへの CommandParameter 未指定/文字列リテラルの Command バインドを禁止する (XamlCommandBindingAnalyzer)
