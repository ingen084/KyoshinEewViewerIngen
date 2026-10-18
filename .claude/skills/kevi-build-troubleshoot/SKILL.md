---
name: kevi-build-troubleshoot
description: KyoshinEewViewerのビルドに必要なgit管理外ディレクトリの用意方法、DesktopプロジェクトのTFM切り替えの仕様、電文系テストの構成と失敗時の調べ方。ビルドエラー（NETSDK1005等）やテスト失敗の調査、git worktreeでのビルド時に使う。
---

# KEViのビルド・テストのトラブルシューティング

## Instructions

### git管理外ディレクトリ (CSVソースジェネレータ)

`KyoshinEewViewer.csproj` のCSVソースジェネレータは、リポジトリ直下の **git管理外** ディレクトリ `jma-code-dictionary/csv/` に依存する。

git worktreeでビルドする場合、このディレクトリは自動では付いてこないため、本体リポジトリのものをJunctionでリンクする必要がある:

```powershell
New-Item -ItemType Junction -Path <worktree>\jma-code-dictionary -Target <本体repo>\jma-code-dictionary
```

これを忘れるとソースジェネレータがCSVを見つけられずビルドが失敗する。

### DesktopプロジェクトのTFMはホストOSで切り替わる

`common.props` は全プロジェクト `net10.0` 単一だが、`KyoshinEewViewer.Desktop` だけは自csproj内の `Condition="$([MSBuild]::IsOSPlatform('Windows'))"` で **Windowsホストのときだけ** `net10.0-windows10.0.19041.0` に上書きされる。

これは意図的な設計で、狙いは「非Windows開発機で `-f` 指定なしの `dotnet run`/F5を可能にしつつ、Windowsでも無駄なnet10.0ビルドとマルチTFM特有の `#if WINDOWS` 並び順ハックを排除する」こと。Desktopは他のどのcsprojからも参照されない葉プロジェクトなので、net10.0出力を必要とする消費者が存在せず、この非対称な上書きが成立する。

これによる既知の挙動:

- **Windows機で `dotnet build -f net10.0` はNETSDK1005で失敗する**。net10.0変種はWindowsホストではrestoreされないためで、バグではない。Windowsではフレームワーク指定なしでビルドする（windows TFMになる）。
- `-p:TargetFramework=net10.0` のグローバルプロパティ指定も不可（restoreがTargetFrameworkグローバルプロパティを無視してwindows TFMをrestoreするため同じくNETSDK1005になる。`-p:TargetFrameworks`併用は推移プロジェクトの評価を壊しMSB4022になる）。
- **Windows機で `#if !WINDOWS` コード（Linux/macOS通知プロバイダ・DBusトレイ検出等）をコンパイル検証したい場合**は、csprojのTFM上書き条件を一時的に `Condition="false"` に書き換えて `dotnet build`（common.propsのnet10.0が有効になりrestoreも通る）。検証後は条件を戻し、`bin\Debug\net10.0` を消して再ビルドすれば元の状態に戻る。この書き換えはコミットしないこと。
- CI（release.yml）は各OSランナー上で `-f` を明示している（windows→windows TFM、ubuntu/mac→net10.0）ので成立する。ローカルとCIでTFMが違って見えても正常。
- 過去にあった `KeviMultiTarget` opt-in・多TFM方式は廃止済み。ビルドエラー対処としてマルチTFM化へ「戻す」修正をしないこと。

### 電文系のテスト

`tests/KyoshinEewViewer.Tests` の電文系（Dmdata/TelegramProvideService系）テストは、以前は約50件が失敗し、フルセットの実行に8分超かかっていた（2026-06-13時点）。dmdataのテストを `Services/Mocks/DmdataTestEnvironment.cs` の模擬通信に置き換えてからは、全件（282件）が約5秒で成功する（2026-10-18確認）。

- 電文系のテストが失敗したら、既存の問題と見なさずに自分の変更由来として調べる
- dmdataのテストを追加するときは `DmdataTestEnvironment.CreatePublisher` を使い、HTTPの応答は `StubHttpHandler` に登録する。未登録の要求は例外になるため、実際の通信は発生しない
- 回線断・dmdataの障害からの復旧など、画面まで通したシナリオは `tests/KyoshinEewViewer.UiTests` の `TelegramRecoveryUiTests` で確認する（実行方法は `tests/UiTests/README.md`）
