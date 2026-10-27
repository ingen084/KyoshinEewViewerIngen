---
name: kevi-ui-tests
description: KyoshinEewViewer(KEVi)のUIテスト (tests/KyoshinEewViewer.UiTests。Avalonia Headlessで実際の画面を操作し、外部通信を禁止して実行する) の実行・追加・失敗調査の手順。UIテストを書く・直す・実行するとき、電文の受信 (回線断・dmdataの障害・WebSocketの切断) のテストを扱うとき、UIテストが不安定・通信監査で失敗するときに使う。
---

# KEViのUIテスト

## Instructions

### 対象

実際のAXAML画面と通常のViewModelをAvalonia Headlessで操作する、小規模な回帰テスト。コンストラクターを省略したりprivateフィールドを書き換えたりしない。対象は初期設定ウィザード、テーマ編集画面、緊急地震速報パネル、災危通報の詳細表示、設定画面からの自動更新、電文の受信 (回線断・dmdataの障害からの復旧)。

自動更新のテストは、更新確認からLinux向けの自己更新処理 (展開・実行権限の付与・旧バージョンの終了を待って置き換え、起動するスクリプト) までを実際に動かす。旧バージョンのプロセスには`sleep`を使い、アプリケーションの終了だけを差し替える。単一ファイル形式の実行ファイルでの更新は `tests/IntegrationTest` の結合テストで確認する。

### 実行

```bash
bash tests/UiTests/run.sh                 # Linux (.NET 10 SDK・Python 3・iproute2・util-linux が必要)
pwsh tests/UiTests/run.ps1                # Windows (WSL の Ubuntu-22.04 で run.sh を動かす)
pwsh tests/UiTests/run.ps1 -Filter 'FullyQualifiedName~TelegramRecovery'   # 一部だけ
```

- `--filter` / `-Filter` は dotnet test のフィルタ。指定した場合は通信禁止の失敗検査を省くので、仕上げには全体を実行する
- `--skip-build` / `-SkipBuild` はビルド済みの同じソースを再検査するときだけ使う
- WSL 側の `dotnet` が .NET 10 でない場合は `--dotnet` / `-DotnetPath` で実行ファイルを指定する
- 結果は `tmp/ui-tests/<日時>-<PID>/` の TRX と `audit/network-audit.json`
- 通常の `dotnet test` で直接実行すると隔離の確認で失敗する。条件を満たさないときに黙ってスキップはしない
- CI は `.github/workflows/ui-tests.yml` (pull_request で Linux の1ジョブ)。macOS のジョブは追加しない
- Claude から実行するときは PowerShell ツールで run.ps1 を呼ぶ。Bash ツール (Git Bash) から `wsl.exe` に `/mnt/...` などのパスを渡すと `C:/Program Files/Git/mnt/...` に変換されるため、`MSYS_NO_PATHCONV=1` を付ける

### 通信禁止の要点

- テスト対象は、ループバックしかない別のLinuxネットワーク名前空間で実行する。UIテストで設定を有効にしても外部へ接続できない
- 専用のApplicationは共通スタイルと画面だけを初期化する。本番のサービス登録・受信開始・Sentry初期化は呼ばない。HOMEは実行ごとの一時プロファイル
- HTTPはテスト用のハンドラー (`FixtureHttpHandler`、電文のテストでは `SimulatedTelegramNetwork`) が応答を生成する。未登録の要求・DNS解決・IPソケット接続は監査 (`OfflineNetworkAttribute`) で検出し、アプリが例外を捕捉してもテストを失敗させる。テストの前後に発生した要求もアセンブリ終了時に検査する
- 全体の実行では、通常のテストのあとに「未登録のテスト用HTTP要求」「直接作成したHttpClient」「DNS」「IPソケット」の4つの失敗検査を各1プロセスで実行し、監査で失敗することを確かめる

仕組みの詳細 (名前空間の作り方、EventSource、子プロセスの扱い) は [references/isolation.md](references/isolation.md)。

### テストを追加するとき

- `[AvaloniaFact]` で実際の画面と通常のViewModelを作る。画面は必ず finally で閉じ、静的なテーマ選択等も復元する
- 実時間のタイマーやバックグラウンド処理は `Ui.WaitUntilAsync` で待つ (UIスレッドのジョブとアニメーションを進めながら待つ)。クリックは `Ui.Click` を使う (ヘッドレス環境では表示が変わった直後のボタンが反応しないことがあり、押されるまで押し直す)
- 数秒〜数分後に動くタイマーは、本体側で `TimeProvider` を受け取れるようにし、`ManualTimeProvider` で進める。タイマーは非同期の処理のあとに予約されることがあるため、まとめて進めずに条件を満たすまで1秒ずつ進める
- HTTPが必要なサービスには `new HttpClient(new FixtureHttpHandler())` を渡して応答を事前登録する。HTTP以外の入力は保存済みデータ・模擬受信・固定時計に差し替える
- テストデータのURLは名前解決できない改変ホスト (`*.fixture.invalid` など。CLAUDE.md の「テストデータのURL規則」)
- 通信設定をOFFにするだけで禁止を実現しない。サービスを追加して要求が漏れると、名前空間で外部接続が止まり、監査でテストが失敗する
- 原版の観測結果を期待値に使う場合も、維持したい仕様かを確認する。調査時の不具合を正解として固定しない

### 電文の受信のテスト

`TelegramRecoveryUiTests` は `TelegramScenario` で、電文の受信先の切り替え (`TelegramProvideService`)、dmdata・防災情報XMLの受信処理、地震情報・津波情報の画面、緊急地震速報の受信状態を本物で組み立て、次だけを模擬する。

- `SimulatedTelegramNetwork`: 回線とdmdata・防災情報XMLの配信。`Online` / `DmdataAvailable` / `WebSocketAvailable` / `ContractListAvailable` で障害を起こし、`Publish` で地震情報を配信し、`RequestCount` で要求を数える
- `FakeDmdataSocketController`: dmdataのWebSocket。DmdataSharpと同じく接続の失敗を例外ではなくイベントで通知する。`Drop` (全接続の切断)、`Reconnect` (DmdataSharpの自動再接続の成功)、`Receive` (電文の受信)
- `FakeOAuthCredential`: dmdataの認可。認可サーバーへは接続しない
- `ManualTimeProvider`: 再接続や復旧確認のタイマーを実時間を待たずに進める

書くときの注意:

- **dmdataを操作する前に `WaitDmdataWebSocketAsync` で受信開始の完了を待つ**。受信開始では情報ごとに過去の電文を取得するため、地震情報の表示だけを待って回線を切ると、津波情報の取得が失敗して防災情報XMLへ切り替わり、テストがときどき失敗する
- 時刻を進めながら待つときは `WaitAsync` や `Wait...Async(advance:)` を使う。一定時間進めるだけなら `AdvanceAsync`
- **「状態が変わらない」ことを確かめる前に `SettleAsync` を挟む**。挟まないと、まだ起きていない切り替えを見逃して誤って成功する
- 防災情報XMLの定期取得 (毎分) は `PollJmaAsync` で起こす
- 模擬データでは過去の電文の取得間隔が1ミリ秒、PULL型の間隔が2秒。PULL型の失敗は前回の間隔で取り直すため、失敗させる前に `WaitPullAttemptsAsync(1)` で通常の間隔の取得を1回挟む
- 防災情報XMLを取得していないことを確かめるときは `GET` だけを数える。受信元を決めるときの確認 (HEAD) は含めない
- 失敗メッセージには `app.Logs` (ログと画面の状態の変化) を含める

### 失敗を調べるとき

- まず失敗メッセージのログで、受信元の切り替えと画面の状態の変化の順序を追う
- 不安定なテストは `-Filter` で対象を10〜15回繰り返し実行して確かめる。1回の成功では安定と判断しない。多くはテストが非同期の処理の完了を待たずに操作していることが原因で、本体の不具合とは限らない
- テストが変更を検出できるかは、変更を1つずつ元に戻したコピーで実行し、対象のテストが失敗することで確かめる
- 通信監査の失敗は `audit/network-audit.json` の `violations` と `unassigned` を見る。テストの終了後に定期取得などで通信すると、アセンブリ終了時の検査で失敗する (`TelegramScenario.DisposeAsync` で受信を止めている)
