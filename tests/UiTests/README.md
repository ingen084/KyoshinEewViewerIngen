# 外部通信を禁止したUIテスト

`tests/KyoshinEewViewer.UiTests` のテストを、外部と通信できないLinuxのネットワーク名前空間で実行します。Avalonia Headlessで実際の画面を操作し、通信が発生したテストは失敗します。

```bash
bash tests/UiTests/run.sh     # Linux (.NET 10 SDK・Python 3・iproute2・util-linux が必要)
pwsh tests/UiTests/run.ps1    # Windows (WSL上の.NET 10 SDKを使います)
```

一部のテストだけを実行するには `--filter` / `-Filter` にdotnet testのフィルタを指定します。通常の`dotnet test`で直接実行すると、隔離の確認で失敗します。

テストの書き方・仕組み・失敗の調べ方は [`.claude/skills/kevi-ui-tests`](../../.claude/skills/kevi-ui-tests/SKILL.md) にまとめています。
