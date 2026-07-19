---
name: kevi-trimming-debug
description: リリースビルド(self-contained + PublishTrimmed)でのみ発生するデシリアライズ不具合の調査手順。トリミング(ILLink)起因のバグ、再現用reproプロジェクトでは再現しないのに本番だけ失敗する場合に使う。
---

# トリミング起因デシリアライズ不具合の調査

## Instructions

GitHub Actionsのリリースビルド（self-contained + `PublishTrimmed=true` / `TrimMode=partial`）でのみ起きるデシリアライズ不具合を調べる際の手順。

### 前提: reproプロジェクトでは再現しないことがある

トリミング(ILLink)の到達可能性解析は**エントリポイント基準**で変わる。`KyoshinEewViewer` をProjectReferenceした別エントリの再現用コンソールプロジェクト（reproプロジェクト）では、`Avalonia.Input.Cursor` 等の依存型のコンストラクタ引数名が保持されてしまい、**本番(Desktopエントリ)で消える不具合が再現しないことがある**。「reproは全部成功するのに本番だけ失敗する」という乖離が実際に起きた。

→ **実Actions相当の検証は、reproプロジェクトを別途作らず `KyoshinEewViewer.Desktop` を必ずエントリにして行う。**

```
dotnet publish src/KyoshinEewViewer.Desktop -p:PublishSingleFile=true --self-contained true
```
（`common.props` でR2R+trimが設定済み）

### GUI起動の不安定さを避ける観測方法

Desktopエントリでの実publishはGUI起動が絡み、多重起動防止(`FocusExistingInstanceOnDuplicate`)・本番サーバー接続・起動時間で不安定になりがち。そこで `Main` 冒頭に一時的な観測モード（例: `--gt-test` のような専用フラグ）を仕込み、`BuildAvaloniaApp().SetupWithoutStarting()` の後、GUIを起動せず対象処理だけ実行してファイルに結果を書き出す方式が有効。

### 例外の可視化

`ConfigurationLoader.TryDeserializeJson` は例外を握り潰す実装になっている。原因究明時は一時的にcatch節へログ出力/ファイル書き出しを追加し、握り潰されている例外を可視化する。

### 過去の実例（参考）

実際にこの手順で見つかった原因は「Trigger/Actionの `DisplayControl`（Control型）がJSON型グラフに混入」というもので、以下の三重対処で解決済み:
- 各overrideへの `[JsonIgnore]`
- `WorkflowDisplayControlAnalyzer`（KEVI001）
- `WorkflowSerializeOption` のControl除外modifier

なお `PublishSingleFile` 自体は無関係（パス解決は `Environment.GetFolderPath` ベースで `Assembly.Location` に依存しないため）。
