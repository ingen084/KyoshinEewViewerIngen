---
name: kevi-build-troubleshoot
description: KyoshinEewViewerのビルドに必要なgit管理外ディレクトリの用意方法、DesktopプロジェクトのTFM切り替えの仕様、既知の失敗するテストの切り分け方法。ビルドエラー（NETSDK1005等）やテスト失敗の調査、git worktreeでのビルド時に使う。
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
- CI（release.yml）は各OSランナー上で `-f` を明示している（windows→windows TFM、ubuntu/mac→net10.0）ので成立する。ローカルとCIでTFMが違って見えても正常。
- 過去にあった `KeviMultiTarget` opt-in・多TFM方式は廃止済み。ビルドエラー対処としてマルチTFM化へ「戻す」修正をしないこと。

### 既知の失敗するテスト（電文系）

`tests/KyoshinEewViewer.Tests` の電文系（Dmdata/TelegramProvideService系）テスト約50件は、**変更前のHEADでも失敗する既存問題**（2026-06-13、HEAD worktreeで確認済み）。タイミング依存の並行テストで、フルセット実行には8分超かかる。

これらの失敗は自分の変更のせいではないことが多いので、震源推定など無関係な変更を検証する際は、フィルタで関係分のみ実行するとよい:

```bash
dotnet test --filter "FullyQualifiedName~Epicenter|FullyQualifiedName~Templates"
```

これなら数十msで完了する。

### 使い分けの判断

テスト失敗の名前が電文系（履歴電文・サブスクライバー・Restore等）を含む場合は、まずこのスキルを参照し、フィルタ実行で自分の変更由来かどうかを判断する。50件の失敗を毎回8分×2（変更前後）で切り分け直すのは無駄。
