---
name: kevi-test-launch
description: KyoshinEewViewer(KEVi)をテスト起動・動作確認する際、専用configプロファイルを指定して本番サーバー(Dmdata/防災科研等)への無用な負荷を避ける手順。「テスト起動」「動作確認」「アプリを起動して確認したい」「/run でKEViを動かす」等の際に使う。
---

# KEViのテスト起動

## Instructions

KyoshinEewViewer.Desktop（または launcher 経由）をテスト起動するときは、**必ず**専用プロファイルディレクトリを `-c` オプションで指定する。

### プロファイルの準備と起動

プロファイルのテンプレートはこのスキルの `templates/` 配下にあり、実行時はgitignore済みの `<repo>\tmp\` 配下へコピーして使う（KEViが実行中に保存するconfig.json/config.json.bak/Logsがgit差分に出ないようにするため）:

```powershell
# 初回またはリセットしたいとき（テンプレートを config.json という名前でコピーする）
New-Item -ItemType Directory -Force <repo>\tmp\test-profile
Copy-Item <repo>\.claude\skills\kevi-test-launch\templates\test-profile.json <repo>\tmp\test-profile\config.json

# 起動
kevi-launcher.exe -c <repo>\tmp\test-profile
```

launcher はコマンドライン引数をKEVi本体に転送するため、launcher経由でも `-c` はそのまま効く。

プロファイルディレクトリが既に存在する場合はコピー不要（前回の状態を引き継いで起動できる）。クリーンな状態から確認したい場合はディレクトリごと削除してテンプレートからコピーし直す。

### なぜ必要か

本番のconfig.json（AppData配下）にはDmdataのRefreshToken等が入っている。これをそのまま使ってテスト起動すると、起動のたびに以下が走り、防災科研やDmdataのサーバーに無用な負荷をかけてしまう:
- 強震モニタの毎秒画像取得
- Dmdata WebSocket再接続
- 観測点取得
- NTP時刻同期

### 用意されているテンプレート

- **`templates/test-profile.json`**: 汎用のテスト起動用。`SeriesEnable`（kyoshin-monitor/earthquake/tsunami/lightning）を全てfalseにし、外部サーバーアクセスを全抑制済み。
- **`templates/test-profile-eq.json`**: earthquakeシリーズのみ有効化した派生版。地震情報レイヤーの描画・ホバー確認用。起動時にJMA XMLフィードの取得が1回走る点が汎用テンプレートと異なる。`<repo>\tmp\test-profile-eq\config.json` へコピーして使う。

各テンプレートには以下を設定済み:
- `SeriesEnable`: 対象シリーズをfalse（デフォルト有効シリーズの無効化）
- `NetworkTime.Enable`: false（TimerServiceがNTP通信せずローカル時刻UTC+9を返す。`TimerService.cs:135` で分岐）
- `Update.Enable`: false（GitHub releaseチェック停止）
- `ShowWizard`: false（初回ウィザードでテストが止まらないように）
- `Logging`: `Enable=true` / `UseCurrentDirectory=true`（`<profile>/Logs` にログが出るので動作検証しやすい）

### 注意点

- KEViは終了時にconfigを完全形で保存し直すため、実行ディレクトリの `config.json` は起動のたびに上書きされる（ただし指定した値は維持される）。テンプレート側は上書きされないので、恒久的に設定を変えたい場合はテンプレートを編集すること。
- テンプレートのファイル名を `config.json` にしないこと（リポジトリの `.gitignore` が `config.json` をグローバルに無視するため追跡できない）。コピー時にリネームする現在の方式を守る。
- `EnableKyoshinMonitor` はEEW用フラグで、強震モニタのリアルタイム取得停止には `SeriesEnable` の方を使うこと（混同しやすい）。
- 新しいシリーズだけを確認したい場合は、既存テンプレートを複製してそのシリーズだけ `SeriesEnable` をtrueにした派生テンプレートを `templates/` に作るとよい（`test-profile-eq.json` が前例）。
