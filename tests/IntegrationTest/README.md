# 起動・自動アップデート結合テスト

起動(スモークテスト)と、更新検出→ダウンロード→検証→自己置換→再起動までの自動アップデートを、
実際に publish したバイナリで検証する結合テスト。CIでは `.github/workflows/integration-test.yml` が
各プラットフォーム(windows/ubuntu/macos × x64/arm64)で実行する。

## 仕組み

- **結合テストビルド**: `dotnet publish -p:IntegrationTestBuild=true` で `INTEGRATION_TEST` 定数が定義され、
  以下のテストフックが有効になる。リリースビルドにはこれらのコードは一切含まれない。
  - 環境変数 `KEVI_UPDATE_API_URL` による更新チェックエンドポイントの差し替え(統計用リクエストも抑止)
  - `--auto-update-test`: 更新検出時に自動で自己更新を開始する起動引数
  - `--smoke-test`: メインウィンドウ表示後にセンチネルファイルを書き出して自動終了する起動引数
  - テストモード時は多重起動チェックとIPCサーバー起動をスキップ
- **センチネルファイル**: 実行ファイルと同じディレクトリの `kevi-startup-sentinel.json`。
  本体(スモークテスト時)とダミー新バージョンが書き出し、テストドライバがポーリングして完了を検出する。
- **ダミー新バージョン** (`UpdateDummy/`): アップデート適用先となる小さなコンソールアプリ。
  起動するとセンチネルを書いて終了する。「新バージョンが実際に正常起動するか」はスモークテスト側が担保する。
- **モックサーバー**: GitHub Releases API形式の `releases.json` とアセットzipを `python -m http.server` で配信する。
  本番のGitHub Releases APIには接続しない。
- **プロファイル** (`profiles/`): 外部サーバーへのアクセスを全て抑止したテスト用config。
  `-c` オプションでプロファイルディレクトリを指定して起動する。

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

# 4. アップデートテスト (pythonが必要)
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
