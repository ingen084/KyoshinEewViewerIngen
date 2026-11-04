# 起動・画面操作・自動アップデート結合テスト

起動(スモークテスト)、画面操作(UIテスト)と、更新検出→ダウンロード→検証→自己置換→再起動までの自動アップデートを、
実際に publish したバイナリで検証する結合テスト。CIではリリース前の検査として、`release.yml` から
`.github/workflows/integration-test.yml` を呼び出し、各プラットフォーム(windows/ubuntu/macos × x64/arm64)で実行する。
1つでも失敗した場合はリリースを作成しない。Actionsの画面から手動で実行することもできる。
ビルド設定 (`*.csproj`、`common.props` など) や UIテストが操作する画面を変える PR でも、Linux x64 だけ同じ検査を実行する。
シナリオで使う文言を変えるとテストが失敗するので、シナリオも合わせて直す。

## 仕組み

- **結合テストビルド**: `dotnet publish -p:IntegrationTestBuild=true` で `INTEGRATION_TEST` 定数が定義され、
  以下のテストフックが有効になる。リリースビルドにはこれらのコードは一切含まれない。
  - 環境変数 `KEVI_UPDATE_API_URL` による更新チェックエンドポイントの差し替え(統計用リクエストも抑止)
  - 環境変数 `KEVI_JMA_FEED_BASE_URL` による防災情報 XML フィードの取得先の差し替え
  - `--auto-update-test`: 更新検出時に自動で自己更新を開始する起動引数
  - `--smoke-test`: メインウィンドウ表示後にセンチネルファイルを書き出し、起動時ワークフローの完了を待って自動終了する起動引数
  - `--ui-test`: メインウィンドウ表示後に画面操作のシナリオを実行し、結果を書き出して自動終了する起動引数
  - `--ui-test-after-restart`: `--ui-test` と併用し、前回の実行で保存した内容が残っていることを確認する起動引数
  - テストモード時は多重起動チェックとIPCサーバー起動をスキップ
  - テストモード時は `PlatformDirectories.ApplicationData` がプロファイル内の `appdata` に切り替わり、Toast 用のレジストリ登録も行わない。
    ユーザーの設定や動作中の本番インスタンスの通知に影響しない
- **センチネルファイル**: 実行ファイルと同じディレクトリの `kevi-startup-sentinel.json`。
  本体(スモークテスト時)とダミー新バージョンが書き出し、テストドライバがポーリングして完了を検出する。
- **ダミー新バージョン** (`UpdateDummy/`): アップデート適用先となる小さなコンソールアプリ。
  起動するとセンチネルを書いて終了する。「新バージョンが実際に正常起動するか」はスモークテスト側が担保する。
- **モックサーバー**: `python -m http.server` で静的ファイルを配信する。アップデートテストは GitHub Releases API形式の
  `releases.json` とアセットzipを、UIテストは `mock-services/` の VOICEVOX の話者一覧と防災情報 XML のフィードと電文を配信する。
  本番のサービスには接続しない。
- **プロファイル** (`profiles/`): 外部サーバーへのアクセスを全て抑止したテスト用config。
  `-c` オプションでプロファイルディレクトリを指定して起動する。
- **テンプレートの確認** (`profiles/workflows-smoke.json`): トリミングはテストプロジェクトでは起きないので、
  スモークテストが実バイナリの起動時ワークフローでテンプレートを実行し、ログの出力を期待値と比べる。
  Scriban の組み込み関数が消えると例外で出力がなくなり、テンプレートが名前で読む .NET 標準型のメンバーが消えると値が空になる。
- **UIテスト** (`src/KyoshinEewViewer.Desktop/IntegrationTest/`): トリミングされた Avalonia の上では `Avalonia.Headless` のテストを実行できないので、
  結合テストビルドのアプリの中にドライバを組み込んで画面を操作する。結果はプロファイルディレクトリの `kevi-ui-test-result.txt` に書き出され、
  最終行が `RESULT: PASSED` でなければ失敗になる。バインディングエラーが1件でも記録された場合も失敗にする。
  同じプロファイルで2回起動し、2回目は保存したワークフローとテーマが残っていることを確認する。
  `run-ui-test.ps1` は環境変数で一時ディレクトリも作業ディレクトリ内に切り替える (電文や音声のキャッシュをユーザーのアプリと共有しない)。
  - ドライバから静的に参照したメンバーはトリミングされなくなるので、確認は表示されたテキストと保存されたファイルで行う。
    名前で読まれるメンバー (`ReflectionBinding` の対象など) をドライバから参照すると、消失を検出できなくなる。
    期待値を名前の表から読まず、シナリオに文字列で書くのもこのため。
  - トリガーかアクションのどちらかを必ず「何もしない」にして、ファイルの実行や通信が動かないようにする。
  - ネイティブのファイル選択画面はプロセス内から操作できないので、過去 XML の読み込みとファイル選択そのものは確認していない。
    地震情報の解析と表示はモックサーバーのフィードから確認する。

  | シナリオ | 確認すること |
  |---|---|
  | `WorkflowScenario` | ワークフローの作成、テスト実行、保存、読み戻し、削除 |
  | `TemplateEditorScenario` | テンプレートエディタの補完と構文エラーの警告 |
  | `WorkflowCatalogScenario` | すべてのトリガーとアクションの選択と表示、選択欄の値の保存と読み戻し |
  | `SettingPagesScenario` | 設定画面のすべてのページの表示、強震モニタと緊急地震速報と災危通報の選択欄 |
  | `VoicevoxScenario` | 話者一覧の取得とメニューの表示 |
  | `ThemeScenario` | 外部テーマの読み込みと選択、テーマ編集画面の編集、破棄、保存 |
  | `EarthquakeScenario` | フィードの受信と電文の解析、地震情報の画面の表示、一覧からの選択 |
  | `QzssScenario` | デバッグメニューからの電文の入力と受信履歴の表示 |

## ローカルでの実行方法 (Windowsの例)

```powershell
# 1. 本体を結合テストビルドでpublish (旧バージョン 0.0.1 相当)
$env:APP_VERSION = '0.0.1'
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj `
    -c Release -r win-x64 -o tmp/local-int/app-raw `
    -p:PublishSingleFile=true --self-contained true -p:IntegrationTestBuild=true
New-Item -ItemType Directory -Force tmp/local-int/dist | Out-Null
Copy-Item tmp/local-int/app-raw/KyoshinEewViewer.Desktop.exe tmp/local-int/dist/KyoshinEewViewer.exe -Force

# 2. ダミー新バージョン (99.0.0) をpublishしてアセットzip化
$env:APP_VERSION = '99.0.0'
dotnet publish tests/IntegrationTest/UpdateDummy/UpdateDummy.csproj `
    -c Release -r win-x64 -o tmp/local-int/dummy-out `
    -p:PublishSingleFile=true --self-contained true
Remove-Item Env:APP_VERSION
pwsh tests/IntegrationTest/package-dummy.ps1 -PublishDir tmp/local-int/dummy-out `
    -Platform windows -OutZip tmp/local-int/KyoshinEewViewer-windows-x64.zip

# 3. スモークテスト
pwsh tests/IntegrationTest/run-smoke-test.ps1 `
    -AppExePath tmp/local-int/dist/KyoshinEewViewer.exe `
    -ProfileTemplate tests/IntegrationTest/profiles/profile-smoke.json `
    -ExpectedVersion 0.0.1

# 4. UIテスト (pythonが必要)
pwsh tests/IntegrationTest/run-ui-test.ps1 `
    -AppExePath tmp/local-int/dist/KyoshinEewViewer.exe `
    -ProfileTemplate tests/IntegrationTest/profiles/profile-ui.json

# 5. アップデートテスト (pythonが必要)
pwsh tests/IntegrationTest/run-update-test.ps1 `
    -AppExePath tmp/local-int/dist/KyoshinEewViewer.exe `
    -ProfileTemplate tests/IntegrationTest/profiles/profile-update.json `
    -NewVersionZip tmp/local-int/KyoshinEewViewer-windows-x64.zip `
    -AssetName KyoshinEewViewer-windows-x64.zip `
    -ExpectedVersion 99.0.0
```

アップデートテスト後は `dist/KyoshinEewViewer.exe` がダミーに置き換わっているため、
再実行する際は手順1の配置からやり直すこと。

macOS の場合は `.app` バンドルの組み立てが必要なため、`.github/actions/publish-kevi/action.yml` の
macos ブランチと同じ手順で `build-files/KyoshinEewViewer.Desktop.app` から組み立て、
`-AppExePath` には `.app/Contents/MacOS/KyoshinEewViewer.Desktop` を指定する。
Linux でヘッドレス環境の場合は `xvfb-run -a` を先頭に付ける。

## 注意

- テストドライバは失敗時にプロファイルディレクトリ配下の `Logs/` を出力する。CIでは `tmp/integration/` を
  artifactとして収集する。
- アセット名やzip内のファイル名は `UpdateCheckService.RidToAssetMap` および各プラットフォームの
  更新処理が期待する名前と一致させる必要がある(`package-dummy.ps1` が担保)。
